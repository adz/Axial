/// Parses rule-specific Axial.Guardrails suppression comments.
///
/// Only ordinary F# line comments are directives:
///
///   // axial-allow-raise-in-flow: foreign callback translates this exception
///
/// A directive names its rule by name (`RuleNames`); the old numeric spelling (`axial-allow-raise-in-flow`) is rejected with
/// the name to use. XML documentation (`///`), block comments, and string contents are not directives. The file-wide form
/// `axial-allow-file-<rule>` must occur in the leading ordinary-comment block. Every directive is recorded in the
/// policy lock, so each new one is a review item.
///
/// raw-effect (AXG001) and raw-concurrency (AXG009) are special: raw effects and raw concurrency belong in files that claim a capability, so their
/// allowances are rare and are highlighted in policy review. One covers one call, never a file, and ends with the gap
/// in Axial or a capability it stands for, or with why the call is deliberate:
///
///   // axial-allow-raw-effect: Axial.Process has no named pipes yet (gap: process-named-pipes)
///   // axial-allow-raw-effect: (why: this test checks the analyzer reports a real Random)
module Axial.Guardrails.Suppressions

open System
open System.Text.RegularExpressions
open FSharp.Compiler.Text

[<Literal>]
let Prefix = "axial-allow-"

type Directive =
    { Line: int
      /// The rule id (AXG001) the directive's rule name stands for.
      RuleId: string option
      Reason: string
      FileWide: bool
      Raw: string }

/// What an AXG001 allowance stands for.
type Allowed =
    /// `(gap: name)`: a feature Axial or a capability is missing.
    | Gap of name: string
    /// `(why: text)`: a deliberate raw call that no feature would replace.
    | Why of text: string

let private gapPattern = Regex(@"\(gap:\s*([A-Za-z0-9][A-Za-z0-9._-]*)\s*\)\s*$", RegexOptions.Compiled)
let private whyPattern = Regex(@"\(why:\s*([^()]*[^()\s])\s*\)\s*$", RegexOptions.Compiled)

/// What an AXG001 allowance stands for, from its trailing `(gap: name)` or `(why: text)`.
let allowanceOf (directive: Directive) =
    let gap = gapPattern.Match directive.Reason
    if gap.Success then Some(Gap gap.Groups[1].Value)
    else
        let why = whyPattern.Match directive.Reason
        if why.Success then Some(Why why.Groups[1].Value) else None

let private validDirective =
    Regex(@"\baxial-allow-(file-)?([A-Za-z][A-Za-z0-9-]*[A-Za-z0-9])\s*:\s*(.*)$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

/// The rule a directive or note names: its id for a known name; the spelling itself (an unknown rule) otherwise. An
/// old numeric spelling stays as written, so integrity reports it with the name to use.
let private ruleOf (spelled: string) =
    if RuleNames.isNumber spelled then spelled.ToUpperInvariant()
    else RuleNames.idOf spelled |> Option.defaultValue (spelled.ToLowerInvariant())

/// The old numeric spelling of a directive or note (`AXG001`), when it uses one.
let numericSpelling (directive: Directive) =
    let matched = Regex.Match(directive.Raw, @"\baxial-(?:allow-(?:file-)?|note\s+)(AXG[0-9]{3})\b", RegexOptions.IgnoreCase)
    if matched.Success then Some(matched.Groups[1].Value.ToUpperInvariant()) else None

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
                  RuleId = Some(ruleOf matched.Groups[2].Value)
                  Reason = matched.Groups[3].Value.Trim()
                  FileWide = matched.Groups[1].Success
                  Raw = comment.Trim() }
        else
            Some { Line = line1; RuleId = None; Reason = ""; FileWide = false; Raw = comment.Trim() }

let allDirectives (sourceText: ISourceText) : Directive list =
    commentsByLine sourceText
    |> List.choose (fun (line1, comment) -> comment |> Option.bind (parseComment line1))

let private notePattern =
    Regex(@"\baxial-note\s+([A-Za-z][A-Za-z0-9-]*[A-Za-z0-9])\s*:\s*(.*)$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

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
                       RuleId = if matched.Success then Some(ruleOf matched.Groups[1].Value) else None
                       Reason = if matched.Success then matched.Groups[2].Value.Trim() else ""
                       FileWide = false; Raw = text.Trim() }))

let guideMessage (config: GuardrailConfig.Config) (ctx: FSharp.Analyzers.SDK.CliContext) (message: FSharp.Analyzers.SDK.Message) =
    match allNotes ctx.SourceText |> List.tryFind (fun note ->
        note.RuleId = Some message.Code && note.Reason <> "" && (numericSpelling note).IsNone
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
            directive.RuleId.IsSome && (numericSpelling directive).IsNone
            && directive.Reason <> ""
            && (not directive.FileWide || isInLeadingCommentBlock sourceText directive.Line)))

/// The rules whose allowances cover one call and must name a gap or a why: raw effects and raw concurrency.
let claimRuleIds = set [ "AXG001"; "AXG009" ]

/// True when a valid suppression for `ruleId` (AXG001) sits on the finding line, the line above it, or (except for AXG001
/// and AXG009) in the file's leading comment block. An AXG001 or AXG009 allowance counts only when it names its gap or why.
let isAllowed (sourceText: ISourceText) line1 (ruleId: string) =
    let ruleId = ruleId.ToUpperInvariant()
    validDirectives sourceText
    |> List.exists (fun directive ->
        let nearby = directive.Line = line1 || directive.Line = line1 - 1
        directive.RuleId = Some ruleId
        && (if claimRuleIds.Contains ruleId then nearby && not directive.FileWide && (allowanceOf directive).IsSome
            else nearby || directive.FileWide))
