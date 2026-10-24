/// Parses rule-specific Axial.Guardrails suppression comments.
///
/// Only ordinary F# line comments are directives:
///
///   // axial-allow-AXG003: foreign callback translates this exception
///
/// XML documentation (`///`), block comments, and string contents are not directives. The file-wide form is
/// `axial-allow-file-AXG001`; it must occur in the leading ordinary-comment block. Both forms are effective only in
/// files declared explicitly with `@(AxialGuardrailsBoundary)`.
module Axial.Guardrails.Suppressions

open System
open System.Text.RegularExpressions
open FSharp.Compiler.Text

[<Literal>]
let Prefix = "axial-allow-"

type Directive =
    { Line: int
      RuleId: string option
      Reason: string
      FileWide: bool
      Raw: string }

let private validDirective =
    Regex(@"\baxial-allow-(file-)?(AXG[0-9]{3})\s*:\s*(.*)$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

type private LexState =
    | Normal
    | TripleString
    | BlockComment of depth: int

let private ordinaryLineComment (state: LexState) (line: string) : LexState * string option =
    let rec scan state index =
        if index >= line.Length then state, None
        else
            match state with
            | BlockComment depth ->
                if index + 1 < line.Length && line[index] = '(' && line[index + 1] = '*' then scan (BlockComment(depth + 1)) (index + 2)
                elif index + 1 < line.Length && line[index] = '*' && line[index + 1] = ')' then
                    if depth = 1 then scan Normal (index + 2) else scan (BlockComment(depth - 1)) (index + 2)
                else scan state (index + 1)
            | TripleString ->
                if index + 2 < line.Length && line.Substring(index, 3) = "\"\"\"" then scan Normal (index + 3)
                else scan state (index + 1)
            | Normal ->
                if index + 2 < line.Length && line.Substring(index, 3) = "\"\"\"" then scan TripleString (index + 3)
                elif index + 1 < line.Length && line[index] = '(' && line[index + 1] = '*' then scan (BlockComment 1) (index + 2)
                elif index + 1 < line.Length && line[index] = '/' && line[index + 1] = '/' then
                    // XML docs explain APIs and may quote suppression syntax; they never suppress code.
                    if index + 2 < line.Length && line[index + 2] = '/' then state, None
                    else state, Some(line.Substring index)
                elif line[index] = '@' && index + 1 < line.Length && line[index + 1] = '"' then
                    let rec verbatim cursor =
                        if cursor >= line.Length then line.Length
                        elif line[cursor] = '"' && cursor + 1 < line.Length && line[cursor + 1] = '"' then verbatim (cursor + 2)
                        elif line[cursor] = '"' then cursor + 1
                        else verbatim (cursor + 1)
                    scan Normal (verbatim (index + 2))
                elif line[index] = '"' then
                    let rec quoted escaped cursor =
                        if cursor >= line.Length then line.Length
                        elif escaped then quoted false (cursor + 1)
                        elif line[cursor] = '\\' then quoted true (cursor + 1)
                        elif line[cursor] = '"' then cursor + 1
                        else quoted false (cursor + 1)
                    scan Normal (quoted false (index + 1))
                else scan state (index + 1)
    scan state 0

let private commentsByLine (sourceText: ISourceText) =
    let mutable state = Normal
    [ for line1 in 1 .. sourceText.GetLineCount() do
          let next, comment = ordinaryLineComment state (sourceText.GetLineString(line1 - 1))
          state <- next
          yield line1, comment ]

let private parseComment line1 (comment: string) =
    if not (comment.Contains(Prefix, StringComparison.OrdinalIgnoreCase)) then None
    else
        let matched = validDirective.Match comment
        if matched.Success then
            Some
                { Line = line1
                  RuleId = Some(matched.Groups[2].Value.ToUpperInvariant())
                  Reason = matched.Groups[3].Value.Trim()
                  FileWide = matched.Groups[1].Success
                  Raw = comment.Trim() }
        else
            Some { Line = line1; RuleId = None; Reason = ""; FileWide = false; Raw = comment.Trim() }

let allDirectives (sourceText: ISourceText) : Directive list =
    commentsByLine sourceText
    |> List.choose (fun (line1, comment) -> comment |> Option.bind (parseComment line1))

let private isInLeadingCommentBlock (sourceText: ISourceText) line1 =
    let ordinaryComments = commentsByLine sourceText |> Map.ofList
    [ 1 .. line1 ]
    |> List.forall (fun n ->
        let trimmed = sourceText.GetLineString(n - 1).Trim()
        trimmed = "" || (ordinaryComments[n].IsSome && sourceText.GetLineString(n - 1).TrimStart().StartsWith("//")))

let validDirectives (sourceText: ISourceText) =
    allDirectives sourceText
    |> List.filter (fun directive ->
        directive.RuleId.IsSome
        && directive.Reason <> ""
        && (not directive.FileWide || isInLeadingCommentBlock sourceText directive.Line))

/// True when a declared boundary file has a valid suppression for `ruleId` on the finding line,
/// the line above it, or in its leading comment block.
let isAllowed (boundaryFiles: Set<string>) (fileName: string) (sourceText: ISourceText) line1 (ruleId: string) =
    let fullPath = IO.Path.GetFullPath fileName
    if not (boundaryFiles.Contains "*" || boundaryFiles.Contains fullPath) then false
    else
        validDirectives sourceText
        |> List.exists (fun directive ->
            directive.RuleId = Some(ruleId.ToUpperInvariant())
            && (directive.FileWide || directive.Line = line1 || directive.Line = line1 - 1))
