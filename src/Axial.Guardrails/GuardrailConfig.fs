/// Which guardrails run, their declared boundary files, and the checked-in violation baseline.
///
/// `build/Axial.Guardrails.targets` writes the resolved MSBuild items to a tab-separated config file:
///
///   guardrail  <name>
///   boundary   <absolute file path>
///   baseline   <rule id>  <repository-relative file>  <fingerprint>[ x<count>]
///   setting    baselineRoot|baselineOutput|nativeAot  <value>
///   rule       <id>  <guardrail>  <category>  <match>  <scope>  <message>  <replacement>
module Axial.Guardrails.GuardrailConfig

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions
open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open Axial.Guardrails.EffectCatalog

[<Literal>]
let EnvironmentVariable = "AXIAL_GUARDRAILS_CONFIG"

type BaselineEntry =
    { RuleId: string
      File: string
      Fingerprint: string }

type Config =
    { /// Enabled guardrails. Findings are errors unless present in Baseline.
      EnabledGuardrails: Set<string>
      NativeAot: bool
      AllRules: EffectRule list
      BoundaryFiles: Set<string>
      BaselineRoot: string
      Baseline: (string * string * string) list
      BaselineOutput: string option }

    member this.Rules = this.AllRules |> List.filter (fun rule -> this.EnabledGuardrails.Contains rule.Guardrail)
    member this.KnownCategories = this.AllRules |> List.map _.Category |> Set.ofList

let private parseScope (text: string) =
    match text.Trim().ToLowerInvariant() with
    | "insideflow" -> InsideFlow
    | "outsideflow" -> OutsideFlow
    | _ -> Anywhere

let defaults: Config =
    { EnabledGuardrails = builtInGuardrails
      NativeAot = false
      AllRules = builtInRules
      // Direct analyzer hosts (not the MSBuild package) have no project item graph; preserve rule-unit-test support.
      BoundaryFiles = Set.singleton "*"
      BaselineRoot = Directory.GetCurrentDirectory()
      Baseline = []
      BaselineOutput = None }

let parse (lines: string seq) : Config =
    let entries = lines |> Seq.map (fun line -> line.Split('\t')) |> Seq.toList

    let enabled =
        entries
        |> List.choose (function
            | [| "guardrail"; name |]
            | [| "guardrail"; name; _ |] -> Some(name.Trim())
            | _ -> None)
        |> Set.ofList

    let contributed =
        entries
        |> List.choose (function
            | [| "rule"; id; guardrail; category; matching; scope; message; replacement |] ->
                parseMatch matching
                |> Option.map (fun parsed ->
                    { Id = id.Trim()
                      Guardrail = guardrail.Trim()
                      Category = category.Trim().ToLowerInvariant()
                      Match = parsed
                      Scope = parseScope scope
                      Message = message.Trim()
                      Replacement = replacement.Trim() })
            | _ -> None)

    let setting name =
        entries
        |> List.tryPick (function
            | [| "setting"; key; value |] when key = name -> Some(value.Trim())
            | _ -> None)

    let nativeAot =
        setting "nativeAot"
        |> Option.exists (fun value -> String.Equals(value, "true", StringComparison.OrdinalIgnoreCase))

    let boundaries =
        entries
        |> List.choose (function
            | [| "boundary"; path |] when path.Trim() <> "" -> Some(Path.GetFullPath(path.Trim()))
            | _ -> None)
        |> Set.ofList

    let baselinePattern = Regex(@"^([0-9a-f]{64})(?: x([2-9][0-9]*))?$")
    let mutable baselineKeys = Set.empty
    let baseline =
        entries
        |> List.collect (function
            | [| "baseline"; ruleId; file; fingerprintAndCount |] ->
                let id = ruleId.Trim()
                let path = file.Trim()
                let value = fingerprintAndCount.Trim()
                let matched = baselinePattern.Match value
                if not matched.Success || id <> id.ToUpperInvariant() || path.Contains('\\') || Path.IsPathRooted path then
                    invalidArg "lines" $"Non-canonical Guardrails baseline entry: {id}\t{path}\t{value}"
                let fingerprint = matched.Groups[1].Value
                let count = if matched.Groups[2].Success then Int32.Parse matched.Groups[2].Value else 1
                let key = id, path, fingerprint
                if baselineKeys.Contains key then
                    invalidArg "lines" $"Duplicate Guardrails baseline entry; use a trailing count: {id}\t{path}\t{fingerprint}"
                baselineKeys <- baselineKeys.Add key
                List.replicate count key
            | _ -> [])

    { EnabledGuardrails = enabled
      NativeAot = nativeAot
      AllRules = builtInRules @ contributed
      BoundaryFiles = boundaries
      BaselineRoot = setting "baselineRoot" |> Option.defaultValue (Directory.GetCurrentDirectory())
      Baseline = baseline
      BaselineOutput = setting "baselineOutput" |> Option.filter ((<>) "") }

let private gate = obj ()
let mutable private cached: (string * DateTime * Config) option = None

let load () : Config =
    match Environment.GetEnvironmentVariable EnvironmentVariable with
    | null
    | "" -> defaults
    | path when not (File.Exists path) -> defaults
    | path ->
        let stamp = File.GetLastWriteTimeUtc path

        lock gate (fun () ->
            match cached with
            | Some(cachedPath, cachedStamp, config) when cachedPath = path && cachedStamp = stamp -> config
            | _ ->
                let config = parse (File.ReadAllLines path)
                cached <- Some(path, stamp, config)
                config)

let private whitespace = Regex(@"\s+", RegexOptions.Compiled)
let private normalize (text: string) = whitespace.Replace(text.Trim(), " ")

let private lineText (sourceText: ISourceText) line1 =
    if line1 >= 1 && line1 <= sourceText.GetLineCount() then sourceText.GetLineString(line1 - 1) else ""

let private offendingText (sourceText: ISourceText) (range: range) =
    if range.StartLine = range.EndLine then
        let line = lineText sourceText range.StartLine
        let startCol = min range.StartColumn line.Length
        let endCol = min range.EndColumn line.Length
        if endCol > startCol then line.Substring(startCol, endCol - startCol) else line
    else
        [ range.StartLine .. range.EndLine ] |> List.map (lineText sourceText) |> String.concat "\n"

let private enclosingAnchor (sourceText: ISourceText) line1 =
    [ line1 .. -1 .. 1 ]
    |> List.tryPick (fun line ->
        let text = (lineText sourceText line).Trim()
        if text.StartsWith("let ") || text.StartsWith("member ") || text.StartsWith("type ") then
            let equals = text.IndexOf '='
            Some(line, if equals >= 0 then text.Substring(0, equals) else text)
        else
            None)
    |> Option.defaultValue (1, "<file>")

let fingerprint (sourceText: ISourceText) (message: Message) =
    let _, anchor = enclosingAnchor sourceText message.Range.StartLine
    let offending = normalize (offendingText sourceText message.Range)
    let material =
        String.concat "\n" [ message.Code.ToUpperInvariant(); normalize anchor; offending ]
    SHA256.HashData(Encoding.UTF8.GetBytes material)
    |> Convert.ToHexString
    |> _.ToLowerInvariant()

let private relativeFile (config: Config) fileName =
    Path.GetRelativePath(config.BaselineRoot, Path.GetFullPath fileName).Replace('\\', '/')

let private appendGenerated (output: string) (entry: BaselineEntry) =
    lock gate (fun () ->
        Directory.CreateDirectory(Path.GetDirectoryName output) |> ignore
        let prefix = $"{entry.RuleId}\t{entry.File}\t{entry.Fingerprint}"
        let lines = if File.Exists output then File.ReadAllLines output |> Array.toList else []
        let mutable found = false
        let updated =
            lines
            |> List.map (fun line ->
                if line = prefix then
                    found <- true
                    prefix + " x2"
                elif line.StartsWith(prefix + " x", StringComparison.Ordinal) then
                    found <- true
                    let count = Int32.Parse(line.Substring(prefix.Length + 2))
                    prefix + $" x{count + 1}"
                else line)
        File.WriteAllLines(output, if found then updated else updated @ [ prefix ]))

/// Applies the configured error severity, downgrading only checked-in baseline entries. In generation mode every
/// finding is also written to the per-project candidate baseline and downgraded so the build can complete.
let applyWith (config: Config) (guardrail: string) (ctx: CliContext) (messages: Message list) : Message list =
    if not (config.EnabledGuardrails.Contains guardrail) then
        []
    else
        let remaining = Collections.Generic.Dictionary<string * string * string, int>()
        for entry in config.Baseline do
            match remaining.TryGetValue entry with
            | true, count -> remaining[entry] <- count + 1
            | _ -> remaining[entry] <- 1

        let findings =
            messages
            |> List.map (fun message ->
                let entry =
                    { RuleId = message.Code.ToUpperInvariant()
                      File = relativeFile config ctx.FileName
                      Fingerprint = fingerprint ctx.SourceText message }
                message, entry)

        let foundCounts =
            findings
            |> List.countBy (fun (_, entry) -> entry.RuleId, entry.File, entry.Fingerprint)
            |> Map.ofList

        findings
        |> List.map (fun (message, entry) ->
            config.BaselineOutput |> Option.iter (fun output -> appendGenerated output entry)
            let key = entry.RuleId, entry.File, entry.Fingerprint
            let baselineCount = remaining.TryGetValue key |> function | true, count -> count | _ -> 0
            let baselined = baselineCount > 0
            if baselined then remaining[key] <- baselineCount - 1
            let foundCount = foundCounts[key]
            let text =
                if not baselined && baselineCount = 0 && config.Baseline |> List.exists ((=) key) then
                    $"{message.Message} ({config.Baseline |> List.filter ((=) key) |> List.length} baselined, {foundCount} found)"
                else message.Message
            { message with Message = text; Severity = if baselined then Severity.Warning else Severity.Error })

let apply (guardrail: string) (ctx: CliContext) (messages: Message list) = applyWith (load ()) guardrail ctx messages

let applyCore (ctx: CliContext) (messages: Message list) = apply CoreGuardrail ctx messages
