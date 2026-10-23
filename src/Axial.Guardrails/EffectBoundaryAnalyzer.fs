/// The `EffectBoundary` analyzer: flags direct calls to ambient .NET effects (System.Random,
/// DateTime.Now/UtcNow, Guid.NewGuid, Console, File/Directory, Environment, Process.Start,
/// Thread.Sleep, new HttpClient, blocking task waits inside a flow) for every enabled guardrail,
/// unless the call site carries an explicit `axial-allow-effect` comment naming the effect category.
///
/// This exists because Axial's architecture invariant — "operational effects are explicit,
/// mockable dependencies visible in a signature" — was violated in practice (`Schedule.jittered`
/// building its own `System.Random`; a fiber diagnostic reading `DateTimeOffset.UtcNow` directly)
/// without any tooling catching it. See dev-docs/current-ideas/api-review.md.
module Axial.Guardrails.EffectBoundaryAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open Axial.Guardrails.EffectCatalog
open Axial.Guardrails.Suppressions

let private matches (rule: EffectRule) (mfv: FSharpMemberOrFunctionOrValue) : bool =
    match rule.Match with
    | ConstructorOf entityFullName ->
        mfv.IsConstructor
        && mfv.DeclaringEntity
           |> Option.map (fun e -> e.TryFullName = Some entityFullName)
           |> Option.defaultValue false
    | MembersOf(entityFullName, memberNames) ->
        mfv.DeclaringEntity
        |> Option.map (fun e -> e.TryFullName = Some entityFullName && List.contains mfv.CompiledName memberNames)
        |> Option.defaultValue false
    | AnyMemberOf entityFullName ->
        mfv.DeclaringEntity
        |> Option.map (fun e -> e.TryFullName = Some entityFullName)
        |> Option.defaultValue false

let private ruleFor (rules: EffectRule list) (insideFlow: bool) (symbol: FSharpSymbol) : EffectRule option =
    match symbol with
    | :? FSharpMemberOrFunctionOrValue as mfv ->
        rules |> List.tryFind (fun r ->
            matches r mfv && (match r.Scope with Anywhere -> true | InsideFlow -> insideFlow | OutsideFlow -> not insideFlow))
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
    match (try ctx.TypedTree with _ -> None) with
    | Some tree -> (try tree.Declarations |> List.collect declarations with _ -> [])
    | None -> []

/// Every ambient-effect call site in the file for the enabled guardrails, regardless of suppression. Exposed so the
/// suppression-integrity analyzer can cross-reference `axial-allow-effect` directives against the findings they're
/// meant to cover, without re-implementing symbol matching.
let rawFindings (ctx: CliContext) : (EffectRule * FSharp.Compiler.Text.range) list =
    let rules = (GuardrailConfig.load ()).Rules
    let flowBodies = lazy (RaiseInFlowAnalyzer.flowBodyRanges ctx)
    let safeReads = completedReadRanges ctx

    ctx.GetAllSymbolUsesOfFile()
    |> Seq.choose (fun symbolUse ->
        let inside = flowBodies.Value |> List.exists (fun body -> contains body symbolUse.Range)
        ruleFor rules inside symbolUse.Symbol
        |> Option.filter (fun rule ->
            rule.Category <> "blocking"
            || not (safeReads |> List.exists (fun read -> contains read symbolUse.Range)))
        |> Option.map (fun rule -> rule, symbolUse.Range))
    |> Seq.toList

let private toMessage (rule: EffectRule) (range: FSharp.Compiler.Text.Range) : Message =
    { Type = "Axial Effect Boundary"
      Message =
        $"This call {rule.Message}. Replace it with the explicit, mockable {rule.Replacement}. "
        + "See the rule documentation when this file intentionally implements the effect boundary."
      Code = "AXG001"
      Severity = Severity.Error
      Range = range
      // Introducing an explicit service changes the containing function's environment and often its return type;
      // replacing only this expression is therefore not mechanically safe.
      Fixes = [] }

[<CliAnalyzer("EffectBoundary",
              "Flags direct use of ambient .NET effects (clock, randomness, GUIDs, console, filesystem, "
              + "process, environment, HTTP clients, blocking waits) that bypass Axial's explicit service boundary.",
              "https://github.com/adz/Axial/blob/main/docs/15-notes/03-guardrails.md")>]
let effectBoundaryAnalyzer: Analyzer<CliContext> =
    fun (ctx: CliContext) ->
        async {
            let config = GuardrailConfig.load ()
            let messages =
                rawFindings ctx
                |> List.collect (fun (rule, range) ->
                    if isAllowed config.BoundaryFiles ctx.FileName ctx.SourceText range.StartLine "AXG001" then
                        []
                    else
                        GuardrailConfig.apply rule.Guardrail ctx [ toMessage rule range ])

            return messages
        }
