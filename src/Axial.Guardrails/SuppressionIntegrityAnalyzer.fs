/// The `SuppressionIntegrity` analyzer: flags `axial-allow-effect`/`axial-allow-effect-file`
/// comments that don't do what they claim.
///
/// A suppression comment is the escape hatch for EffectBoundary (AXG001), and it's the thing an
/// agent chasing a green build will reach for first once it exists. Two ways it goes wrong
/// silently: the named category is misspelled (`// axial-allow-effect: guide` matches nothing in
/// `EffectCatalog.knownCategories`, so it suppresses nothing and the AXG001 finding just... isn't
/// there, unexplained), or the suppression is orphaned - it once covered a real call site, the
/// call was refactored away or moved, and the comment now silences nothing and misleads the next
/// reader into thinking this location is a known effect boundary.
module Axial.Guardrails.SuppressionIntegrityAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open Axial.Guardrails.EffectCatalog
open Axial.Guardrails.Suppressions
open Axial.Guardrails.EffectBoundaryAnalyzer

let private message (range: range) (text: string) : Message =
    { Type = "Axial Suppression Integrity"
      Message = text
      Code = "AXG002"
      Severity = Severity.Warning
      Range = range
      Fixes = [] }

let private rangeOfLine (sourceText: FSharp.Compiler.Text.ISourceText) (line1: int) : range =
    let lineText = if line1 >= 1 && line1 <= sourceText.GetLineCount() then sourceText.GetLineString(line1 - 1) else ""
    let endCol = lineText.Length
    Range.mkRange "" (Position.mkPos line1 0) (Position.mkPos line1 endCol)

/// A line-level directive at `directiveLine` covers a finding at that same line, or the line
/// below it - matching `Suppressions.lineLevelAllowedCategories`, which checks the flagged line
/// itself and the line above.
let private coversLine (directiveLine: int) (findingLine: int) : bool =
    findingLine = directiveLine || findingLine = directiveLine + 1

let private unknownCategoryFindings
    (sourceText: FSharp.Compiler.Text.ISourceText)
    (directiveKind: string)
    (directives: (int * Set<string>) list)
    : Message list =
    [ for line1, categories in directives do
          for category in categories do
              if not (Set.contains category knownCategories) then
                  yield
                      message
                          (rangeOfLine sourceText line1)
                          ($"This {directiveKind} names unknown category '{category}', which matches nothing in "
                           + "Axial.Guardrails' effect catalog - it suppresses nothing. Check for a typo against "
                           + "the categories in docs/15-notes/03-guardrails.md.") ]

let private orphanedLineDirectiveFindings
    (sourceText: FSharp.Compiler.Text.ISourceText)
    (raw: (EffectRule * range) list)
    (directives: (int * Set<string>) list)
    : Message list =
    [ for line1, categories in directives do
          for category in categories do
              if Set.contains category knownCategories then
                  let hasMatch =
                      raw
                      |> List.exists (fun (rule, range) -> rule.Category = category && coversLine line1 range.StartLine)

                  if not hasMatch then
                      yield
                          message
                              (rangeOfLine sourceText line1)
                              ($"This `// axial-allow-effect: {category}` suppression doesn't cover any "
                               + $"'{category}' call on this line or the line below it. It's either stale (the "
                               + "call it once covered was removed or moved) or was never matching - either way, "
                               + "remove it or move it next to the call it's meant to allow.") ]

let private orphanedFileDirectiveFindings
    (sourceText: FSharp.Compiler.Text.ISourceText)
    (raw: (EffectRule * range) list)
    (directives: (int * Set<string>) list)
    : Message list =
    [ for line1, categories in directives do
          for category in categories do
              if Set.contains category knownCategories then
                  let hasMatch = raw |> List.exists (fun (rule, _) -> rule.Category = category)

                  if not hasMatch then
                      yield
                          message
                              (rangeOfLine sourceText line1)
                              ($"This `// axial-allow-effect-file: {category}` suppression doesn't cover any "
                               + $"'{category}' call anywhere in this file. It's either stale or was never "
                               + "matching - remove it, or narrow the file-level directive to the categories the "
                               + "file actually needs.") ]

[<CliAnalyzer("SuppressionIntegrity",
              "Flags axial-allow-effect suppression comments that name an unknown category, or that "
              + "no longer cover any actual effect call - both signs of a stale or mistyped suppression.",
              "https://github.com/adz/Axial/blob/main/docs/guardrails.md")>]
let suppressionIntegrityAnalyzer: Analyzer<CliContext> =
    fun (ctx: CliContext) ->
        async {
            let sourceText = ctx.SourceText
            let raw = rawFindings ctx
            let lineDirectives = allLineDirectives sourceText
            let fileDirectives = allFileDirectives sourceText

            return
                [ yield! unknownCategoryFindings sourceText "// axial-allow-effect" lineDirectives
                  yield! unknownCategoryFindings sourceText "// axial-allow-effect-file" fileDirectives
                  yield! orphanedLineDirectiveFindings sourceText raw lineDirectives
                  yield! orphanedFileDirectiveFindings sourceText raw fileDirectives ]
        }
