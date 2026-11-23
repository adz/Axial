/// The `EffectBoundary` analyzer: flags direct calls to ambient .NET effects (System.Random,
/// DateTime.Now/UtcNow, Guid.NewGuid, Console, File/Directory, Environment, Process.Start,
/// Thread.Sleep, new HttpClient, blocking task waits inside a flow) for every enabled guardrail. `EffectUse.judge`
/// allows a call in a file that claims the owning capability, in a host file, or under a gap allowance.
///
/// This exists because Axial's architecture invariant — "operational effects are explicit,
/// mockable dependencies visible in a signature" — was violated in practice (`Schedule.jittered`
/// building its own `System.Random`; a fiber diagnostic reading `DateTimeOffset.UtcNow` directly)
/// without any tooling catching it. See dev-docs/current-ideas/api-review.md.
module Axial.Guardrails.EffectBoundaryAnalyzer

open System.Collections.Generic
open System.Runtime.CompilerServices
open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open Axial.Guardrails.EffectCatalog
open Axial.Guardrails.Suppressions

let private entityOf (effect: SymbolMatch) =
    match effect with
    | ConstructorOf entityFullName | MembersOf(entityFullName, _) | AnyMemberOf entityFullName -> entityFullName

/// The rest of a match, once the declaring entity is known to equal the rule's.
let private matchesMember (rule: EffectRule) (mfv: FSharpMemberOrFunctionOrValue) : bool =
    match rule.Match with
    | ConstructorOf _ -> mfv.IsConstructor
    | MembersOf(_, memberNames) -> List.contains mfv.CompiledName memberNames
    | AnyMemberOf _ -> true

// Every rule names its declaring entity, so a file's symbol uses look their candidates up by that name instead of
// trying each rule in turn. Built once per rule list; each entity's rules keep catalog order, so the first match wins
// as before.
let private indexes = ConditionalWeakTable<EffectRule list, Dictionary<string, EffectRule list>>()

let private index (rules: EffectRule list) =
    indexes.GetValue(rules, fun rules ->
        let byEntity = Dictionary<string, EffectRule list>()
        for rule in List.rev rules do
            let entity = entityOf rule.Match
            byEntity[entity] <- rule :: (match byEntity.TryGetValue entity with | true, existing -> existing | _ -> [])
        byEntity)

let private ruleFor (rules: EffectRule list) (insideFlow: Lazy<bool>) (symbol: FSharpSymbol) : EffectRule option =
    match symbol with
    | :? FSharpMemberOrFunctionOrValue as mfv ->
        match mfv.DeclaringEntity |> Option.bind _.TryFullName with
        | Some entity ->
            match (index rules).TryGetValue entity with
            | true, candidates ->
                candidates |> List.tryFind (fun r ->
                    matchesMember r mfv
                    && (match r.Scope with Anywhere -> true | InsideFlow -> insideFlow.Value | OutsideFlow -> not insideFlow.Value))
            | _ -> None
        | None -> None
    | _ -> None

let private contains (outer: FSharp.Compiler.Text.range) (inner: FSharp.Compiler.Text.range) =
    FSharp.Compiler.Text.Range.rangeContainsRange outer inner

let private completionMembers = set [ "get_IsCompleted"; "get_IsCompletedSuccessfully"; "get_IsFaulted"; "get_IsCanceled"; "get_Status"; "IsCompleted"; "IsCompletedSuccessfully"; "IsFaulted"; "IsCanceled"; "Status" ]

let rec private valueOf expr =
    match expr with
    | Value value -> Some value
    | AddressOf inner -> valueOf inner
    | _ -> None

let rec private completionValues expr =
    let here =
        match expr with
        | Call(Some receiver, memberValue, _, _, _) when completionMembers.Contains memberValue.CompiledName ->
            valueOf receiver |> Option.toList
        | _ -> []
    here @ (expr.ImmediateSubExpressions |> List.collect completionValues)

let rec private awaiterReceiver expr =
    match expr with
    | Call(Some receiver, getAwaiter, _, _, _) when getAwaiter.CompiledName = "GetAwaiter" -> valueOf receiver
    | Call(None, getAwaiter, _, _, receiver :: _) when getAwaiter.CompiledName = "GetAwaiter" -> valueOf receiver
    | _ -> expr.ImmediateSubExpressions |> List.tryPick awaiterReceiver

let private readReceiver expr =
    match expr with
    | Call(Some receiver, memberValue, _, _, _) when memberValue.CompiledName = "get_Result" || memberValue.CompiledName = "Result" ->
        valueOf receiver
    | Call(receiver, memberValue, _, _, arguments) when memberValue.CompiledName = "GetResult" ->
        // The awaiter is a struct, so F# often copies it into a local first; that local is aliased in `walk`.
        match (receiver |> Option.toList) @ arguments |> List.tryPick awaiterReceiver with
        | Some value -> Some value
        | None -> receiver |> Option.bind valueOf
    | _ -> None

let private completedReadRanges (ctx: CliContext) =
    let rec continuationParameters expr =
        match expr with
        | Lambda(parameter, body) -> parameter :: continuationParameters body
        | _ -> expr.ImmediateSubExpressions |> List.collect continuationParameters
    // Expressions the compiler service built with error recovery (a project it could not fully resolve, as CI saw
    // for executables) throw when their patterns are read. Skip them: an unreadable expression only loses its
    // exemptions, so the finding stays, and the analyzer never crashes the build.
    let rec walk guarded continuation expr =
        try walkChecked guarded continuation expr with _ -> []
    and walkChecked guarded continuation expr =
        let here =
            match readReceiver expr with
            | Some value when List.exists ((=) value) (guarded @ continuation) -> [ expr.Range ]
            | _ -> []
        let children =
            match expr with
            | IfThenElse(condition, yes, no) ->
                walk guarded continuation condition
                @ walk (completionValues condition @ guarded) continuation yes
                @ walk guarded continuation no
            | Let((local, bound, _), body) ->
                // `let copy = x.GetAwaiter()` under a completion guard on x: the copy is guarded too.
                let aliased =
                    match awaiterReceiver bound with
                    | Some value when List.exists ((=) value) (guarded @ continuation) -> [ local ]
                    | _ -> []
                walk guarded continuation bound @ walk (aliased @ guarded) continuation body
            | DecisionTree(decision, targets) ->
                walk guarded continuation decision
                @ (targets |> List.collect (fun (_, body) -> walk guarded continuation body))
            | Call(_, memberValue, _, _, arguments) when memberValue.CompiledName = "ContinueWith" ->
                arguments |> List.collect (fun argument ->
                    walk guarded (continuationParameters argument @ continuation) argument)
            | _ -> expr.ImmediateSubExpressions |> List.collect (walk guarded continuation)
        here @ children
    let rec declarations declaration =
        match declaration with
        | FSharpImplementationFileDeclaration.Entity(_, members) -> members |> List.collect declarations
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(_, _, body)
        | FSharpImplementationFileDeclaration.InitAction body -> (try walk [] [] body with _ -> [])
    try SymbolIndex.declarations ctx |> List.collect declarations with _ -> []

/// Every ambient-effect call site in the file for the enabled guardrails, regardless of suppression. Exposed so the
/// suppression-integrity analyzer can cross-reference AXG001 directives against the findings they're
/// meant to cover, without re-implementing symbol matching.
let private collectFindings rules (ctx: CliContext) : (EffectRule * FSharp.Compiler.Text.range) list =
    let flowBodies = lazy (RaiseInFlowAnalyzer.flowBodyRanges ctx)
    let safeReads = lazy (completedReadRanges ctx)

    (SymbolIndex.forContext ctx).All
    // A definition is not a use: a declared capability may wrap a type the same project defines.
    |> Seq.filter (fun symbolUse -> not symbolUse.IsFromDefinition)
    |> Seq.choose (fun symbolUse ->
        let inside = lazy (flowBodies.Value |> List.exists (fun body -> contains body symbolUse.Range))
        ruleFor rules inside symbolUse.Symbol
        |> Option.filter (fun rule ->
            rule.Capability <> Some "blocking"
            || not (safeReads.Value |> List.exists (fun read -> contains read symbolUse.Range)))
        |> Option.map (fun rule -> rule, symbolUse.Range))
    |> Seq.toList

let rawFindingsWithConfig (config: GuardrailConfig.Config) (ctx: CliContext) : (EffectRule * FSharp.Compiler.Text.range) list =
    SymbolIndex.memo ctx ("AXG001.findings", config.Rules) (fun () -> collectFindings config.Rules ctx)

let rawFindings ctx = rawFindingsWithConfig (GuardrailConfig.load ()) ctx

let toMessage config fileName rule range = EffectUse.toMessage config fileName rule range

/// Every raw effect call in the file with its verdict, for findings and for the `effectUses` record.
let judged (config: GuardrailConfig.Config) (ctx: CliContext) =
    rawFindingsWithConfig config ctx
    |> List.map (fun (rule, range) -> rule, range, EffectUse.judge config ctx.FileName ctx.SourceText range.StartLine rule)

let analyzeWithConfig (config: GuardrailConfig.Config) (ctx: CliContext) =
    if GuardrailConfig.isSkippedSource config ctx.FileName then [] else
    judged config ctx
    |> List.collect (fun (rule, range, verdict) ->
        match verdict with
        | EffectUse.Reported -> GuardrailConfig.applyWith config rule.Guardrail ctx [ toMessage config ctx.FileName rule range ]
        | _ -> [])

/// Full records its own exact count; Fast records the syntactic one.
let private uses (ctx: CliContext) =
    let config = GuardrailConfig.load ()
    if GuardrailConfig.isSkippedSource config ctx.FileName then []
    else [ EffectUse.record (judged config ctx |> List.map (fun (_, _, verdict) -> verdict)) ]

[<CliAnalyzer("EffectBoundary",
              "Flags direct use of ambient .NET effects (clock, randomness, GUIDs, console, filesystem, "
              + "process, environment, HTTP clients, blocking waits) that bypass Axial's explicit service boundary.",
              "https://github.com/adz/Axial/blob/main/docs/15-notes/04-guardrail-diagnostics.md#axg001-use-explicit-effect-services")>]
let effectBoundaryAnalyzer (ctx: CliContext) =
    let run (ctx: CliContext) =
        async {
            return analyzeWithConfig (GuardrailConfig.load ()) ctx
        }
    Execution.trackWith uses "EffectBoundary" run ctx
