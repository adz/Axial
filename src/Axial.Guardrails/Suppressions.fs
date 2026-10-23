/// Parses rule-specific Axial.Guardrails suppression comments.
///
/// A suppression is deliberately verbose so a reviewer can see both the rule and the reason:
///
///   // axial-allow-AXG003: foreign callback translates this exception
///
/// `axial-allow-file-AXG001` is the file-wide form. File-wide directives must be in the leading
/// comment block. Both forms are effective only in files declared with `@(AxialGuardrailsBoundary)`.
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

let private textOfLine1 (sourceText: ISourceText) (line1: int) =
    if line1 >= 1 && line1 <= sourceText.GetLineCount() then
        sourceText.GetLineString(line1 - 1)
    else
        ""

let private parseLine line1 (text: string) =
    if not (text.Contains(Prefix, StringComparison.OrdinalIgnoreCase)) then
        None
    else
        let matched = validDirective.Match text

        if matched.Success then
            Some
                { Line = line1
                  RuleId = Some(matched.Groups[2].Value.ToUpperInvariant())
                  Reason = matched.Groups[3].Value.Trim()
                  FileWide = matched.Groups[1].Success
                  Raw = text.Trim() }
        else
            Some
                { Line = line1
                  RuleId = None
                  Reason = ""
                  FileWide = false
                  Raw = text.Trim() }

let allDirectives (sourceText: ISourceText) : Directive list =
    [ for line1 in 1 .. sourceText.GetLineCount() do
          match parseLine line1 (textOfLine1 sourceText line1) with
          | Some directive -> yield directive
          | None -> () ]

let private isInLeadingCommentBlock (sourceText: ISourceText) line1 =
    [ 1 .. line1 ]
    |> List.forall (fun n ->
        let trimmed = (textOfLine1 sourceText n).TrimStart()
        trimmed = "" || trimmed.StartsWith("//", StringComparison.Ordinal))

let validDirectives (sourceText: ISourceText) =
    allDirectives sourceText
    |> List.filter (fun directive ->
        directive.RuleId.IsSome
        && directive.Reason <> ""
        && (not directive.FileWide || isInLeadingCommentBlock sourceText directive.Line))

/// True when a declared boundary file has a valid suppression for `ruleId` on the finding line,
/// the line above it, or in its leading comment block.
let isAllowed
    (boundaryFiles: Set<string>)
    (fileName: string)
    (sourceText: ISourceText)
    (line1: int)
    (ruleId: string)
    =
    let fullPath = IO.Path.GetFullPath fileName

    if not (boundaryFiles.Contains "*" || boundaryFiles.Contains fullPath) then
        false
    else
        validDirectives sourceText
        |> List.exists (fun directive ->
            directive.RuleId = Some(ruleId.ToUpperInvariant())
            && (if directive.FileWide then true else directive.Line = line1 || directive.Line = line1 - 1))
