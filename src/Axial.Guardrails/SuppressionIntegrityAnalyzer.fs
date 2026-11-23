/// Validates every Axial.Guardrails suppression comment. Suppressions are accepted only when they
/// identify one rule, include a reason, and cover a finding. An AXG001 allowance also covers one call and names the
/// gap or why it stands for; tiered rules accept only visible guide acknowledgements.
module Axial.Guardrails.SuppressionIntegrityAnalyzer

open System.IO
open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open Axial.Guardrails.GuardrailConfig
open Axial.Guardrails.Suppressions
open Axial.Guardrails.EffectBoundaryAnalyzer

let private rangesForRule (config: Config) (ctx: CliContext) ruleId =
    match ruleId with
    | "AXG001" -> rawFindingsWithConfig config ctx |> List.map snd
    | "AXG003" -> RaiseInFlowAnalyzer.rawRanges ctx
    | "AXG004" -> FixtureAnalyzer.rawRanges ctx
    | "AXG005" -> DiscardedCancellationAnalyzer.rawRanges ctx
    | "AXG006" -> ReflectionFormattingAnalyzer.rawRangesWithNativeAot config.NativeAot ctx
    | "AXG007" -> ResourceFromFiberAnalyzer.rawRanges ctx
    | "AXG008" -> ScopeFromRunAnalyzer.rawRanges ctx
    | "AXG009" | "AXG010" | "AXG011" | "AXG012" -> ConcurrencyHazardsAnalyzer.rawRanges ruleId ctx
    | "AXG013" -> WaitForFactsAnalyzer.rawRanges ctx
    | _ -> []

let private knownRuleIds = set [ for number in 1 .. 14 -> sprintf "AXG%03d" number ]

let private message (sourceText: ISourceText) (range: range) (text: string) : Message =
    let line = sourceText.GetLineString(range.StartLine - 1)
    let fixes =
        if line.TrimStart().StartsWith("//") then
            [ { FromRange = range; FromText = line; ToText = "" } ]
        else
            []
    { Type = "Axial Suppression Integrity"
      Message = text
      Code = "AXG002"
      Severity = Severity.Error
      Range = range
      Fixes = fixes }

let private rangeOfLine (sourceText: ISourceText) line1 =
    let text = if line1 >= 1 && line1 <= sourceText.GetLineCount() then sourceText.GetLineString(line1 - 1) else ""
    Range.mkRange "" (Position.mkPos line1 0) (Position.mkPos line1 text.Length)

let private isLeadingComment (sourceText: ISourceText) line1 =
    [ 1 .. line1 ]
    |> List.forall (fun line ->
        let text = sourceText.GetLineString(line - 1).TrimStart()
        text = "" || text.StartsWith "//")

let private coversLine directiveLine findingLine = findingLine = directiveLine || findingLine = directiveLine + 1

/// Why an AXG001 allowance next to raw calls allows none of them, if it allows none. An allowance counts only when it
/// is what allows a call: a claim or a host file already allows the call, and a forbid level overrides it.
let private unneededAllowance (config: Config) (ctx: CliContext) directive =
    let covered =
        EffectBoundaryAnalyzer.judged config ctx
        |> List.filter (fun (_, range: FSharp.Compiler.Text.range, _) -> coversLine directive.Line range.StartLine)
        |> List.map (fun (_, _, verdict) -> verdict)
    if covered.IsEmpty || covered |> List.exists (function EffectUse.Allowance _ -> true | _ -> false) then None
    else
        match covered |> List.tryPick (function EffectUse.Owned name -> Some name | _ -> None) with
        | Some capability ->
            Some $"Remove this allowance: this file claims the {capability} capability, so the call needs no comment."
        | None when covered |> List.contains EffectUse.Host ->
            Some "Remove this allowance: this is a host file, which may make raw calls without a comment."
        | None ->
            Some "Remove this allowance: the effects.ambient level here is forbid, which no allowance overrides. Use the service instead."

let private findingForDirective (config: Config) (ctx: CliContext) directive =
    let range = rangeOfLine ctx.SourceText directive.Line
    let report = message ctx.SourceText range

    match directive.RuleId with
    | None ->
        Some(report "Remove this malformed suppression, or replace it with a rule-specific suppression and a non-empty reason as documented.")
    | Some ruleId when not (knownRuleIds.Contains ruleId) ->
        Some(report $"Remove this suppression or replace its unknown rule id `{ruleId}` with the diagnostic id it is intended to allow.")
    | Some _ when directive.Reason = "" ->
        Some(report "Add a non-empty reason explaining why this intentional boundary cannot use the recommended API.")
    | Some ruleId when Set.contains ruleId (set [ "AXG009"; "AXG010"; "AXG011"; "AXG012"; "AXG013"; "AXG014" ]) ->
        Some(report "Remove this allowance: tiered rules cannot be suppressed. Only guide findings accept `axial-note AXGnnn: reason` acknowledgements.")
    | Some _ when directive.FileWide && not (isLeadingComment ctx.SourceText directive.Line) ->
        Some(report "Move this file-wide suppression into the leading comment block, before the first declaration.")
    | Some "AXG001" when directive.FileWide ->
        Some(report "Remove this file-wide allowance: a file that implements a capability claims it on its Compile item (AxialCapability=\"<name>\"), and may then call that API without comments.")
    | Some "AXG001" when (allowanceOf directive).IsNone ->
        Some(report "End this allowance with the gap it stands for, such as `(gap: process-named-pipes)` for a missing Axial or capability feature, or with `(why: reason)` for a deliberate raw call. If the file implements a capability, claim it on its Compile item (AxialCapability=\"<name>\") instead.")
    | Some "AXG001" when (unneededAllowance config ctx directive).IsSome ->
        unneededAllowance config ctx directive |> Option.map report
    | Some ruleId ->
        let covered =
            rangesForRule config ctx ruleId
            |> List.exists (fun findingRange -> directive.FileWide || coversLine directive.Line findingRange.StartLine)
        if covered then None
        else Some(report $"Remove this stale suppression or move it next to the `{ruleId}` finding it intentionally allows.")

let private findingForNote (config: Config) (ctx: CliContext) (note: Directive) =
    let live =
        match note.RuleId with
        | Some "AXG010" -> ConcurrencyHazardsAnalyzer.analyzeWithConfig config "AXG010" ctx |> List.map _.Range
        | Some "AXG013" ->
            let guides = WaitForFactsAnalyzer.guideRanges ctx
            WaitForFactsAnalyzer.analyzeWithConfig config ctx
            |> List.choose (fun m -> if guides |> List.exists (fun r -> r.Start = m.Range.Start && r.End = m.Range.End) then Some m.Range else None)
        | Some "AXG014" -> TaskSurfaceAnalyzer.liveRanges config ctx
        | _ -> []
    if note.Reason <> "" && live |> List.exists (fun r -> coversLine note.Line r.StartLine) then None
    else
        let diagnostic = message ctx.SourceText (rangeOfLine ctx.SourceText note.Line)
                            "Remove this invalid note: for AXG010 put the note as the first line inside the handler body, immediately before its first expression (not above 'with' or the pattern). For AXG013 and AXG014 use the finding's line or the preceding line. Block findings cannot be acknowledged."
        Some { diagnostic with Type = "Axial Note Integrity"; Severity = if config.Checkpoint then Severity.Error else Severity.Warning }

/// A claim on a file that makes no raw call for that capability: a reviewed permission nothing uses. A warning, at the
/// top of the file, naming the project file whose Compile item carries the claim.
let private unusedClaims (config: Config) (ctx: CliContext) =
    let used =
        EffectBoundaryAnalyzer.judged config ctx
        |> List.choose (fun (rule: Axial.Guardrails.EffectCatalog.EffectRule, _, _) -> rule.Capability)
        |> Set.ofList
    let project = config.ProjectPath |> Option.map Path.GetFileName |> Option.defaultValue "the project file"
    config.AllCapabilities
    |> List.filter (fun capability ->
        EffectUse.owns ctx.FileName capability
        && config.EnabledGuardrails.Contains capability.Guardrail
        && not (used.Contains capability.Name))
    |> List.map (fun capability ->
        { Type = "Axial Claim Integrity"
          Message =
            $"Remove the {capability.Name} claim from this file's Compile item in {project} (AxialCapability): the file "
            + $"makes no raw {capability.Name} call, so the claim allows nothing."
          Code = "AXG002"
          Severity = Severity.Warning
          Range = rangeOfLine ctx.SourceText 1
          Fixes = [] })

let analyzeWithConfig (config: Config) (ctx: CliContext) =
    if GuardrailConfig.isSkippedSource config ctx.FileName then []
    else
        (allDirectives ctx.SourceText |> List.choose (findingForDirective config ctx))
        @ (allNotes ctx.SourceText |> List.choose (findingForNote config ctx))
        @ unusedClaims config ctx
        |> applyWith config Axial.Guardrails.EffectCatalog.CoreGuardrail ctx

[<CliAnalyzer("SuppressionIntegrity",
              "Flags invalid Axial.Guardrails suppressions and guide acknowledgements.",
              "https://github.com/adz/Axial/blob/main/docs/15-notes/04-guardrail-diagnostics.md#axg002-remove-invalid-suppressions")>]
let suppressionIntegrityAnalyzer (ctx: CliContext) =
    let run (ctx: CliContext) =
        async {
            return analyzeWithConfig (load ()) ctx
        }
    Execution.track "SuppressionIntegrity" run ctx
