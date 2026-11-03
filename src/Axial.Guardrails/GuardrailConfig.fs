/// Which guardrails run, their declared boundary files, and the checked-in violation baseline.
///
/// `build/Axial.Guardrails.targets` writes the resolved MSBuild items to a tab-separated config file:
///
///   guardrail  <name>
///   boundary   <absolute file path>
///   baseline   <rule id>  <repository-relative file>  <fingerprint>[ x<count>]
///   setting    baselineRoot|baselineOutput|nativeAot|checkpoint  <value>
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
      Fingerprint: string
      Count: int }

type Config =
    { /// Enabled guardrails. AXG001–008 use Baseline; tiered findings keep their analyzer severity.
      EnabledGuardrails: Set<string>
      NativeAot: bool
      Checkpoint: bool
      InTests: string
      AllRules: EffectRule list
      BoundaryFiles: Set<string>
      BaselineRoot: string
      Baseline: BaselineEntry list
      BaselineOutput: string option
      BaselineRule: string option
      Policy: Policy.Config option
      PolicyRoot: string option
      ProjectPath: string option }

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
      Checkpoint = false
      InTests = ""
      AllRules = builtInRules
      // Direct analyzer hosts (not the MSBuild package) have no project item graph; preserve rule-unit-test support.
      BoundaryFiles = Set.singleton "*"
      BaselineRoot = Directory.GetCurrentDirectory()
      Baseline = []
      BaselineOutput = None
      BaselineRule = None
      Policy = None
      PolicyRoot = None
      ProjectPath = None }

let parse (lines: string seq) : Config =
    let entries = lines |> Seq.mapi (fun index line -> index + 1, line, line.Split('\t')) |> Seq.toList

    let enabled =
        entries
        |> List.choose (function
            | _, _, [| "guardrail"; name |]
            | _, _, [| "guardrail"; name; _ |] -> Some(name.Trim())
            | _ -> None)
        |> Set.ofList

    let contributed =
        entries
        |> List.choose (function
            | _, _, [| "rule"; id; guardrail; category; matching; scope; message; replacement |] ->
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
            | _, _, [| "setting"; key; value |] when key = name -> Some(value.Trim())
            | _ -> None)

    let nativeAot =
        setting "nativeAot"
        |> Option.exists (fun value -> String.Equals(value, "true", StringComparison.OrdinalIgnoreCase))

    let boundaries =
        entries
        |> List.choose (function
            | _, _, [| "boundary"; path |] when path.Trim() <> "" ->
                try Some(Path.GetFullPath(path.Trim())) with _ -> None
            | _ -> None)
        |> Set.ofList

    let baselinePattern = Regex(@"^([0-9a-f]{64})(?: x([2-9]|[1-9][0-9]{1,3}|10000))?$")
    let ruleIdPattern = Regex(@"^AXG[0-9]{3}$")
    let mutable baselineKeys = Set.empty
    let malformed lineNumber =
        invalidArg "lines" $"Non-canonical Guardrails baseline line {lineNumber}"
    let baseline =
        entries
        |> List.choose (function
            | _, _, [| "baseline"; value |] when value = "" || value.StartsWith("#", StringComparison.Ordinal) -> None
            | lineNumber, _, [| "baseline"; ruleId; file; fingerprintAndCount |] ->
                let matched = baselinePattern.Match fingerprintAndCount
                let pathSegments = file.Split('/')
                if not matched.Success
                   || not (ruleIdPattern.IsMatch ruleId)
                   || file.StartsWith("/", StringComparison.Ordinal)
                   || file.Contains('\\')
                   || file |> Seq.exists Char.IsWhiteSpace
                   || pathSegments |> Array.exists (fun segment -> segment = "" || segment = "." || segment = "..") then
                    malformed lineNumber
                let fingerprint = matched.Groups[1].Value
                let count = if matched.Groups[2].Success then Int32.Parse matched.Groups[2].Value else 1
                let key = ruleId, file, fingerprint
                if baselineKeys.Contains key then
                    invalidArg "lines" $"Duplicate Guardrails baseline entry on line {lineNumber}; use a trailing count"
                baselineKeys <- baselineKeys.Add key
                Some { RuleId = ruleId; File = file; Fingerprint = fingerprint; Count = count }
            | lineNumber, _, fields when fields.Length > 0 && fields[0] = "baseline" -> malformed lineNumber
            | _ -> None)

    let policyRoot = setting "policyRoot" |> Option.filter ((<>) "")
    let policy =
        match policyRoot, setting "policyFile" with
        | Some root, Some path when File.Exists path ->
            let known = set [ "effects.ambient"; "domain.async-return"; "domain.whole-environment"; "domain.service-provider"; "test.integrity"; "test.wait-for-facts" ]
            match Policy.parse root known (File.ReadAllText path) with
            | Ok parsed -> Some parsed
            | Error errors -> invalidArg "lines" (String.concat Environment.NewLine errors)
        | _ -> None

    { EnabledGuardrails = enabled
      NativeAot = nativeAot
      Checkpoint = setting "checkpoint" |> Option.exists (fun value -> String.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
      InTests = setting "inTests" |> Option.defaultValue ""
      AllRules = builtInRules @ contributed
      BoundaryFiles =
        match policy, policyRoot with
        | Some parsed, Some root -> parsed.Boundaries |> List.map (fun path -> Path.GetFullPath(Path.Combine(root, path))) |> Set.ofList
        | _ -> boundaries
      BaselineRoot = setting "baselineRoot" |> Option.defaultValue (Directory.GetCurrentDirectory())
      Baseline = baseline
      BaselineOutput = setting "baselineOutput" |> Option.filter ((<>) "")
      BaselineRule = setting "baselineRule" |> Option.filter ((<>) "")
      Policy = policy
      PolicyRoot = policyRoot
      ProjectPath = setting "projectPath" |> Option.filter ((<>) "") }

let private gate = obj ()
let mutable private cached: (string * DateTime * Config) option = None
/// Fail closed even in standalone hosts and on repeated loads; stderr alone cannot fail an analyzer host.
let rejectRemovedSeverity () =
    let value = Environment.GetEnvironmentVariable "AXIAL_GUARDRAILS_SEVERITY"
    let propertyStyle = Environment.GetEnvironmentVariable "AxialGuardrailsSeverity"
    if not (String.IsNullOrWhiteSpace value) || not (String.IsNullOrWhiteSpace propertyStyle) then
        invalidOp "error AXIALGR001: AxialGuardrailsSeverity / AXIAL_GUARDRAILS_SEVERITY is removed in Axial 0.12. Remove it and configure supported per-role rule levels in Axial.Guardrails.toml. See the policy-roles migration row in the 0.12.0 release notes (dev-docs/releases/0.12.0.md#policy-roles). AXG009-013 retain their fixed block/guide policy; AxialGuardrailsCheckpoint controls unacknowledged guides at checkpoints."

let load () : Config =
    rejectRemovedSeverity ()
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
    Path.GetRelativePath(config.BaselineRoot, fileName).Replace('\\', '/')

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

let private warned = Collections.Generic.HashSet<string>()

let private warnSkipped fileName explanation =
    lock gate (fun () ->
        if warned.Add fileName then
            eprintfn "Axial.Guardrails: skipping source '%s': %s" fileName explanation)

/// Resolve a compiler-supplied path against an explicit base instead of the process current directory.
let private sourceBaseDirectory (config: Config) =
    config.ProjectPath
    |> Option.bind (fun project ->
        try Some(Path.GetDirectoryName(Path.GetFullPath project)) with _ -> None)

let private classifyConfiguredSource (config: Config) root fileName =
    match sourceBaseDirectory config with
    | Some baseDirectory -> Policy.classifySourceFrom baseDirectory root fileName
    | None -> Policy.classifySourceFrom root root fileName

/// Classify a compiler source against the configured policy root. With no policy root the
/// standalone analyzer behavior is unchanged.
let sourceClassification (config: Config) fileName =
    config.PolicyRoot
    |> Option.map (fun root -> classifyConfiguredSource config root fileName)

let isSkippedSource (config: Config) fileName =
    match sourceClassification config fileName with
    | Some(Policy.ExternalSource) -> true
    | Some(Policy.InvalidSource explanation) ->
        warnSkipped fileName explanation
        true
    | _ -> false

let private tryRelative root path =
    try Some(Path.GetRelativePath(root, Path.GetFullPath path).Replace('\\', '/'))
    with _ -> None

/// Applies the enabled guardrail and legacy baseline policy. Tiered findings and note validation retain their
/// resolved severity and never generate baseline entries.
let resolvedPolicy (config: Config) (ctx: CliContext) =
    match config.Policy, config.PolicyRoot, config.ProjectPath with
    | Some policy, Some root, Some project ->
        match classifyConfiguredSource config root ctx.FileName, tryRelative root project with
        | Policy.RepositorySource file, Some projectFile ->
            match Policy.resolve policy projectFile file with
            | Ok resolved -> Some resolved
            | Error error -> invalidArg "policy" error
        | Policy.ExternalSource, _ -> None
        | Policy.InvalidSource explanation, _ ->
            warnSkipped ctx.FileName explanation
            None
        | _, None ->
            warnSkipped project "project path is unusable"
            None
    | _ -> None

// Only the declared project role selects the reduced set; file-role overrides cannot opt a project out.
let usesTestAnalyzerSet (config: Config) =
    if String.Equals(config.InTests, "all", StringComparison.OrdinalIgnoreCase) then false else
    match config.Policy, config.PolicyRoot, config.ProjectPath with
    | Some policy, Some root, Some project ->
        match tryRelative root project with
        | Some relative ->
            match Policy.projectRole policy relative with
            | Ok role -> role = Policy.Test
            | Error error -> invalidArg "policy" error
        | None -> false
    | _ -> false

/// Selection happens before a rule touches symbols or typed trees; AXG002 still validates directives.
let shouldRunAnalyzer config rule =
    not (usesTestAnalyzerSet config)
    || List.contains rule [ "SuppressionIntegrity"; "Fixture"; "RawConcurrency"; "WaitForFacts" ]

let private resolvableFullPath (config: Config) (fileName: string) =
    try
        let basePath =
            match sourceBaseDirectory config with
            | Some directory -> directory
            | None -> defaultArg config.PolicyRoot config.BaselineRoot
        Some(Path.GetFullPath(fileName, basePath))
    with _ -> None

let isBoundaryFile (config: Config) fileName =
    config.BoundaryFiles.Contains "*"
    || (resolvableFullPath config fileName |> Option.exists config.BoundaryFiles.Contains)

let applyWith (config: Config) (guardrail: string) (ctx: CliContext) (messages: Message list) : Message list =
    if isSkippedSource config ctx.FileName then [] else
    let sourcePath = resolvableFullPath config ctx.FileName
    if sourcePath.IsNone then
        warnSkipped ctx.FileName "source path is unusable"
        []
    else
    let policy = resolvedPolicy config ctx
    let level = policy |> Option.bind (fun resolved -> resolved.Rules.TryFind "effects.ambient") |> Option.defaultValue Policy.Deny
    if not (config.EnabledGuardrails.Contains guardrail) then
        []
    else
        let remaining = Collections.Generic.Dictionary<string * string * string, int>()
        for entry in config.Baseline do
            remaining[(entry.RuleId, entry.File, entry.Fingerprint)] <- entry.Count

        let testSet = usesTestAnalyzerSet config
        let findings =
            messages
            |> List.filter (fun message ->
                not testSet
                || List.contains message.Code [ "AXG002"; "AXG004"; "AXG009"; "AXG013" ])
            |> List.map (fun message ->
                let entry =
                    { RuleId = message.Code.ToUpperInvariant()
                      File = relativeFile config sourcePath.Value
                      Fingerprint = fingerprint ctx.SourceText message
                      Count = 1 }
                message, entry)

        let foundCounts =
            findings
            |> List.countBy (fun (_, entry) -> entry.RuleId, entry.File, entry.Fingerprint)
            |> Map.ofList

        findings
        |> List.map (fun (message, entry) ->
            let tiered = Set.contains message.Code (set [ "AXG009"; "AXG010"; "AXG011"; "AXG012"; "AXG013" ]) || message.Type = "Axial Note Integrity"
            let candidate = not tiered && (not (message.Code = "AXG001") || level <> Policy.Allow)
                            && (config.BaselineRule |> Option.forall ((=) message.Code))
            if candidate then config.BaselineOutput |> Option.iter (fun output -> appendGenerated output entry)
            let key = entry.RuleId, entry.File, entry.Fingerprint
            let baselineCount = remaining.TryGetValue key |> function | true, count -> count | _ -> 0
            let baselined = baselineCount > 0
            if baselined then remaining[key] <- baselineCount - 1
            let foundCount = foundCounts[key]
            let text =
                match config.Baseline |> List.tryFind (fun entry -> (entry.RuleId, entry.File, entry.Fingerprint) = key) with
                | Some entry when not baselined && baselineCount = 0 ->
                    $"{message.Message} ({entry.Count} baselined, {foundCount} found)"
                | _ -> message.Message
            let ambient = message.Code = "AXG001"
            let severity =
                if tiered then message.Severity
                elif baselined || (ambient && level = Policy.Warn)
                     || (candidate && config.BaselineOutput.IsSome && config.BaselineRule.IsSome) then Severity.Warning
                else Severity.Error
            { message with Message = (if tiered then message.Message else text); Severity = severity })
        |> List.filter (fun message -> message.Code <> "AXG001" || level <> Policy.Allow)

let apply (guardrail: string) (ctx: CliContext) (messages: Message list) = applyWith (load ()) guardrail ctx messages

let applyCore (ctx: CliContext) (messages: Message list) = apply CoreGuardrail ctx messages
