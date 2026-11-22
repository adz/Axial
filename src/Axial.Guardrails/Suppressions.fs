/// Parses rule-specific Axial.Guardrails suppression comments.
///
/// Only ordinary F# line comments are directives:
///
///   // axial-allow-AXG003: foreign callback translates this exception
///
/// XML documentation (`///`), block comments, and string contents are not directives. The file-wide form
/// `axial-allow-file-AXGnnn` must occur in the leading ordinary-comment block. Every directive is recorded in the
/// policy lock, so each new one is a review item.
///
/// AXG001 is special: raw effects belong in a capability's claimed files, so an AXG001 allowance is a recorded gap in
/// Axial or a capability. It covers one call, never a file, and ends with the gap it stands for:
///
///   // axial-allow-AXG001: Axial.Process has no named pipes yet (gap: process-named-pipes)
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

let private gapPattern = Regex(@"\(gap:\s*([A-Za-z0-9][A-Za-z0-9._-]*)\s*\)\s*$", RegexOptions.Compiled)

/// The gap an AXG001 allowance names with a trailing `(gap: name)`, if it names one.
let gapOf (directive: Directive) =
    let matched = gapPattern.Match directive.Reason
    if matched.Success then Some matched.Groups[1].Value else None

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

let private lexComments (sourceText: ISourceText) =
    let mutable state = Normal
    [ for line1 in 1 .. sourceText.GetLineCount() do
          let next, comment = ordinaryLineComment state (sourceText.GetLineString(line1 - 1))
          state <- next
          yield line1, comment ]

// Every finding asks whether it is allowed, and every guide message whether it is acknowledged; lex each source once.
let private comments = System.Runtime.CompilerServices.ConditionalWeakTable<ISourceText, (int * string option) list>()

let private commentsByLine (sourceText: ISourceText) = comments.GetValue(sourceText, fun text -> lexComments text)

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

let private notePattern =
    Regex(@"\baxial-note\s+(AXG[0-9]{3})\s*:\s*(.*)$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

/// Notes acknowledge guide findings, never suppress them. Reuse the ordinary-comment lexer so quoted
/// documentation and string contents cannot acknowledge a finding.
let allNotes (sourceText: ISourceText) : Directive list =
    commentsByLine sourceText
    |> List.choose (fun (line, comment) ->
        comment |> Option.bind (fun text ->
            if not (text.Contains("axial-note", StringComparison.OrdinalIgnoreCase)) then None
            else
                let matched = notePattern.Match text
                Some { Line = line
                       RuleId = if matched.Success then Some(matched.Groups[1].Value.ToUpperInvariant()) else None
                       Reason = if matched.Success then matched.Groups[2].Value.Trim() else ""
                       FileWide = false; Raw = text.Trim() }))

let guideMessage (config: GuardrailConfig.Config) (ctx: FSharp.Analyzers.SDK.CliContext) (message: FSharp.Analyzers.SDK.Message) =
    match allNotes ctx.SourceText |> List.tryFind (fun note ->
        note.RuleId = Some message.Code && note.Reason <> ""
        && (note.Line = message.Range.StartLine || note.Line + 1 = message.Range.StartLine)) with
    | Some note -> { message with Severity = FSharp.Analyzers.SDK.Severity.Info; Message = $"{message.Message} Acknowledged: {note.Reason}" }
    | None -> { message with Severity = if config.Checkpoint then FSharp.Analyzers.SDK.Severity.Error else FSharp.Analyzers.SDK.Severity.Warning }

let private isInLeadingCommentBlock (sourceText: ISourceText) line1 =
    let ordinaryComments = commentsByLine sourceText |> Map.ofList
    [ 1 .. line1 ]
    |> List.forall (fun n ->
        let trimmed = sourceText.GetLineString(n - 1).Trim()
        trimmed = "" || (ordinaryComments[n].IsSome && sourceText.GetLineString(n - 1).TrimStart().StartsWith("//")))

let private valid = System.Runtime.CompilerServices.ConditionalWeakTable<ISourceText, Directive list>()

let validDirectives (sourceText: ISourceText) =
    valid.GetValue(sourceText, fun sourceText ->
        allDirectives sourceText
        |> List.filter (fun directive ->
            directive.RuleId.IsSome
            && directive.Reason <> ""
            && (not directive.FileWide || isInLeadingCommentBlock sourceText directive.Line)))

/// True when a valid suppression for `ruleId` sits on the finding line, the line above it, or (except for AXG001)
/// in the file's leading comment block. An AXG001 allowance counts only when it names its gap.
let isAllowed (sourceText: ISourceText) line1 (ruleId: string) =
    let ruleId = ruleId.ToUpperInvariant()
    validDirectives sourceText
    |> List.exists (fun directive ->
        let nearby = directive.Line = line1 || directive.Line = line1 - 1
        directive.RuleId = Some ruleId
        && (if ruleId = "AXG001" then nearby && not directive.FileWide && (gapOf directive).IsSome
            else nearby || directive.FileWide))
