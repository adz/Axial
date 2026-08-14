/// The `EffectBoundary` analyzer: flags direct calls to ambient .NET effects (System.Random,
/// DateTime.Now/UtcNow, Guid.NewGuid, Console, File/Directory, Environment, Process.Start,
/// Thread.Sleep) unless the call site carries an explicit `axial-allow-effect` comment naming
/// the effect category.
///
/// This exists because Axial's architecture invariant — "operational effects are explicit,
/// mockable dependencies visible in a signature" — was violated in practice (`Schedule.jittered`
/// building its own `System.Random`; `FiberRegistry.Dump()` reading `DateTimeOffset.UtcNow`)
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

let private ruleFor (symbol: FSharpSymbol) : EffectRule option =
    match symbol with
    | :? FSharpMemberOrFunctionOrValue as mfv -> rules |> List.tryFind (fun r -> matches r mfv)
    | _ -> None

/// Every ambient-effect call site in the file, regardless of suppression. Exposed so the
/// suppression-integrity analyzer can cross-reference `axial-allow-effect` directives against
/// the findings they're meant to cover, without re-implementing symbol matching.
let rawFindings (ctx: CliContext) : (EffectRule * FSharp.Compiler.Text.range) list =
    ctx.GetAllSymbolUsesOfFile()
    |> Seq.choose (fun symbolUse -> ruleFor symbolUse.Symbol |> Option.map (fun rule -> rule, symbolUse.Range))
    |> Seq.toList

let private toMessage (rule: EffectRule) (range: FSharp.Compiler.Text.Range) : Message =
    { Type = "Axial Effect Boundary"
      Message =
        $"This call {rule.Message}. Axial core packages must route this through an explicit, "
        + $"mockable service: use {rule.Replacement}. If this line *is* the intended boundary "
        + $"implementation, mark it explicitly: `// axial-allow-effect: {rule.Category}` on this "
        + "line or the line above, or `// axial-allow-effect-file: "
        + $"{rule.Category}` in the file header if the whole file is the boundary."
      Code = "AXG001"
      Severity = Severity.Warning
      Range = range
      Fixes = [] }

[<CliAnalyzer("EffectBoundary",
              "Flags direct use of ambient .NET effects (clock, randomness, GUIDs, console, filesystem, "
              + "process, environment) that bypass Axial's explicit service boundary.",
              "https://github.com/adz/Axial/blob/main/docs/guardrails.md")>]
let effectBoundaryAnalyzer: Analyzer<CliContext> =
    fun (ctx: CliContext) ->
        async {
            let fileCategories = fileLevelAllowedCategories ctx.SourceText

            let messages =
                rawFindings ctx
                |> Seq.filter (fun (rule, range) ->
                    not (isAllowed ctx.SourceText fileCategories range.StartLine rule.Category))
                |> Seq.map (fun (rule, range) -> toMessage rule range)
                |> Seq.toList

            return messages
        }
