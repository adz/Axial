/// Validates every Axial.Guardrails suppression comment. Suppressions are accepted only when they
/// identify one rule, include a reason, and cover a finding. AXG001–008 retain boundary allowances;
/// tiered rules accept only visible guide acknowledgements.
module Axial.Guardrails.SuppressionIntegrityAnalyzer

open System.IO
open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open Axial.Guardrails.GuardrailConfig
open Axial.Guardrails.Suppressions
open Axial.Guardrails.EffectBoundaryAnalyzer

let private rangesForRule (ctx: CliContext) ruleId =
    match ruleId with
    | "AXG001" -> rawFindings ctx |> List.map snd
    | "AXG003" -> RaiseInFlowAnalyzer.rawRanges ctx
    | "AXG004" -> FixtureAnalyzer.rawRanges ctx
    | "AXG005" -> DiscardedCancellationAnalyzer.rawRanges ctx
    | "AXG006" -> ReflectionFormattingAnalyzer.rawRangesWithNativeAot (load ()).NativeAot ctx
    | "AXG007" -> ResourceFromFiberAnalyzer.rawRanges ctx
    | "AXG008" -> ScopeFromRunAnalyzer.rawRanges ctx
    | "AXG009" | "AXG010" | "AXG011" | "AXG012" -> ConcurrencyHazardsAnalyzer.rawRanges ruleId ctx
    | "AXG013" -> WaitForFactsAnalyzer.rawRanges ctx
    | _ -> []

let private knownRuleIds = set [ for number in 1 .. 13 -> sprintf "AXG%03d" number ]

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

let private findingForDirective (ctx: CliContext) directive =
    let config = load ()
    let range = rangeOfLine ctx.SourceText directive.Line
    let report = message ctx.SourceText range
    let boundary = config.BoundaryFiles.Contains "*" || config.BoundaryFiles.Contains(Path.GetFullPath ctx.FileName)

    match directive.RuleId with
    | None ->
        Some(report "Remove this malformed suppression, or replace it with a rule-specific suppression and a non-empty reason as documented.")
    | Some ruleId when not (knownRuleIds.Contains ruleId) ->
        Some(report $"Remove this suppression or replace its unknown rule id `{ruleId}` with the diagnostic id it is intended to allow.")
    | Some _ when directive.Reason = "" ->
        Some(report "Add a non-empty reason explaining why this intentional boundary cannot use the recommended API.")
    | Some ruleId when Set.contains ruleId (set [ "AXG009"; "AXG010"; "AXG011"; "AXG012"; "AXG013" ]) ->
        Some(report "Remove this allowance: tiered rules cannot be suppressed. Only guide findings accept `axial-note AXGnnn: reason` acknowledgements.")
    | Some _ when directive.FileWide && not (isLeadingComment ctx.SourceText directive.Line) ->
        Some(report "Move this file-wide suppression into the leading comment block, before the first declaration.")
    | Some _ when not boundary ->
        Some(report "Remove this suppression: suppressions are allowed only in files declared with `AxialGuardrailsBoundary`.")
    | Some ruleId ->
        let covered =
            rangesForRule ctx ruleId
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
        | _ -> []
    if note.Reason <> "" && live |> List.exists (fun r -> coversLine note.Line r.StartLine) then None
    else
        let diagnostic = message ctx.SourceText (rangeOfLine ctx.SourceText note.Line)
                            "Remove this invalid note: use `axial-note AXG010: reason` or `axial-note AXG013: reason` on a live guide finding's line or the preceding line. Block findings cannot be acknowledged."
        Some { diagnostic with Type = "Axial Note Integrity"; Severity = if config.Checkpoint then Severity.Error else Severity.Warning }

[<CliAnalyzer("SuppressionIntegrity",
              "Flags invalid Axial.Guardrails suppressions and guide acknowledgements.",
              "https://github.com/adz/Axial/blob/main/docs/15-notes/04-guardrail-diagnostics.md#axg002-remove-invalid-suppressions")>]
let suppressionIntegrityAnalyzer: Analyzer<CliContext> =
    fun ctx ->
        async {
            let config = load ()
            return
                (allDirectives ctx.SourceText |> List.choose (findingForDirective ctx))
                @ (allNotes ctx.SourceText |> List.choose (findingForNote config ctx))
                |> applyWith config Axial.Guardrails.EffectCatalog.CoreGuardrail ctx
        }
