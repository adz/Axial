/// Parses `axial-allow-effect` suppression comments out of source text.
///
/// Two forms are recognized:
///
///   // axial-allow-effect: clock
///
/// placed on the flagged line, or the line immediately above it, allows that one call site for
/// the named category (or categories, comma-separated). It does not silence any other category
/// at that site, and does not silence the category anywhere else in the file.
///
///   // axial-allow-effect-file: clock, random
///
/// placed anywhere in the file's leading comment block (before the first non-comment,
/// non-blank line) allows every call site in the file for the named categories. This is for
/// files whose entire purpose is to be the explicit boundary around an effect, such as an
/// Axial.PlatformService live implementation.
module Axial.Guardrails.Suppressions

open System
open System.Text.RegularExpressions
open FSharp.Compiler.Text

let private lineDirective =
    Regex(@"axial-allow-effect:\s*([A-Za-z0-9,\-\s]+)", RegexOptions.Compiled)

let private fileDirective =
    Regex(@"axial-allow-effect-file:\s*([A-Za-z0-9,\-\s]+)", RegexOptions.Compiled)

let private categoriesOf (m: Match) : Set<string> =
    m.Groups[1].Value.Split(',')
    |> Array.map (fun s -> s.Trim().ToLowerInvariant())
    |> Array.filter (fun s -> s <> "")
    |> Set.ofArray

/// Categories suppressed at the given 1-based source line, from that line or the line above it.
let lineLevelAllowedCategories (sourceText: ISourceText) (line1: int) : Set<string> =
    let textOfLine1 (n: int) =
        if n >= 1 && n <= sourceText.GetLineCount() then
            sourceText.GetLineString(n - 1)
        else
            ""

    let onThisLine = lineDirective.Match(textOfLine1 line1)
    let onLineAbove = lineDirective.Match(textOfLine1 (line1 - 1))

    let fromMatch (m: Match) =
        if m.Success then categoriesOf m else Set.empty

    Set.union (fromMatch onThisLine) (fromMatch onLineAbove)

/// Categories suppressed for the whole file, read from the leading comment block: every
/// contiguous line from the top of the file that is blank or starts with `//`.
let fileLevelAllowedCategories (sourceText: ISourceText) : Set<string> =
    let lineCount = sourceText.GetLineCount()

    let rec headerLines n acc =
        if n > lineCount then
            List.rev acc
        else
            let text = sourceText.GetLineString(n - 1)
            let trimmed = text.TrimStart()

            if trimmed = "" || trimmed.StartsWith("//") then
                headerLines (n + 1) (text :: acc)
            else
                List.rev acc

    headerLines 1 []
    |> List.map fileDirective.Match
    |> List.filter (fun m -> m.Success)
    |> List.map categoriesOf
    |> List.fold Set.union Set.empty

/// True when `category` is allowed at `line1` by either a line-level or file-level directive.
let isAllowed (sourceText: ISourceText) (fileCategories: Set<string>) (line1: int) (category: string) : bool =
    Set.contains category fileCategories
    || Set.contains category (lineLevelAllowedCategories sourceText line1)

/// Every line-level `axial-allow-effect` directive in the file, as (1-based line it appears on,
/// the categories it names). Used by the suppression-integrity check to find directives that
/// don't correspond to any real finding, or that name an unknown category.
let allLineDirectives (sourceText: ISourceText) : (int * Set<string>) list =
    [ for line1 in 1 .. sourceText.GetLineCount() do
          let m = lineDirective.Match(sourceText.GetLineString(line1 - 1))

          if m.Success then
              let categories = categoriesOf m

              if not (Set.isEmpty categories) then
                  yield line1, categories ]

/// Every file-level `axial-allow-effect-file` directive in the file's leading comment block, as
/// (1-based line it appears on, the categories it names).
let allFileDirectives (sourceText: ISourceText) : (int * Set<string>) list =
    let lineCount = sourceText.GetLineCount()

    let rec headerLines n acc =
        if n > lineCount then
            List.rev acc
        else
            let text = sourceText.GetLineString(n - 1)
            let trimmed = text.TrimStart()

            if trimmed = "" || trimmed.StartsWith("//") then
                headerLines (n + 1) ((n, text) :: acc)
            else
                List.rev acc

    [ for line1, text in headerLines 1 [] do
          let m = fileDirective.Match(text)

          if m.Success then
              let categories = categoriesOf m

              if not (Set.isEmpty categories) then
                  yield line1, categories ]
