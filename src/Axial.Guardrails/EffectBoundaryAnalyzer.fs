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

let private ruleFor (rules: EffectRule list) (symbol: FSharpSymbol) : EffectRule option =
    match symbol with
    | :? FSharpMemberOrFunctionOrValue as mfv -> rules |> List.tryFind (fun r -> matches r mfv)
    | _ -> None

let private contains (outer: FSharp.Compiler.Text.range) (inner: FSharp.Compiler.Text.range) =
    FSharp.Compiler.Text.Range.rangeContainsRange outer inner

/// Every ambient-effect call site in the file for the enabled guardrails, regardless of suppression. Exposed so the
/// suppression-integrity analyzer can cross-reference `axial-allow-effect` directives against the findings they're
/// meant to cover, without re-implementing symbol matching.
let rawFindings (ctx: CliContext) : (EffectRule * FSharp.Compiler.Text.range) list =
    let rules = (GuardrailConfig.load ()).Rules
    let flowBodies = lazy (RaiseInFlowAnalyzer.flowBodyRanges ctx)

    ctx.GetAllSymbolUsesOfFile()
    |> Seq.choose (fun symbolUse -> ruleFor rules symbolUse.Symbol |> Option.map (fun rule -> rule, symbolUse.Range))
    |> Seq.filter (fun (rule, range) ->
        match rule.Scope with
        | Anywhere -> true
        | InsideFlow -> flowBodies.Value |> List.exists (fun body -> contains body range))
    |> Seq.toList

let private toMessage (severity: Severity) (rule: EffectRule) (range: FSharp.Compiler.Text.Range) : Message =
    { Type = "Axial Effect Boundary"
      Message =
        $"This call {rule.Message}. The {rule.Guardrail} guardrail asks for an explicit, mockable "
        + $"alternative: use {rule.Replacement}. If this line *is* the intended boundary "
        + $"implementation, mark it explicitly: `// axial-allow-effect: {rule.Category}` on this "
        + "line or the line above, or `// axial-allow-effect-file: "
        + $"{rule.Category}` in the file header if the whole file is the boundary."
      Code = "AXG001"
      Severity = severity
      Range = range
      Fixes = [] }

[<CliAnalyzer("EffectBoundary",
              "Flags direct use of ambient .NET effects (clock, randomness, GUIDs, console, filesystem, "
              + "process, environment, HTTP clients, blocking waits) that bypass Axial's explicit service boundary.",
              "https://github.com/adz/Axial/blob/main/docs/15-notes/03-guardrails.md")>]
let effectBoundaryAnalyzer: Analyzer<CliContext> =
    fun (ctx: CliContext) ->
        async {
            let config = GuardrailConfig.load ()
            let fileCategories = fileLevelAllowedCategories ctx.SourceText

            let messages =
                rawFindings ctx
                |> Seq.filter (fun (rule, range) ->
                    not (isAllowed ctx.SourceText fileCategories range.StartLine rule.Category))
                |> Seq.map (fun (rule, range) ->
                    toMessage (config.Severities.TryFind rule.Guardrail |> Option.defaultValue Severity.Warning) rule range)
                |> Seq.toList

            return messages
        }
