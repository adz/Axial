/// Which guardrails run, their declared boundary files, and the checked-in violation baseline.
///
/// `build/Axial.Guardrails.targets` writes the resolved MSBuild items to a tab-separated config file:
///
///   guardrail  <name>
///   boundary   <absolute file path>
///   baseline   <rule id>  <repository-relative file>  <fingerprint>[ x<count>]
///   setting    baselineRoot|baselineOutput|nativeAot|checkpoint|severityCap  <value>
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
      /// Framework MSBuild's Full fallback for a Fast request: findings report no more severely than Fast (no Error).
      FastSeverityCap: bool
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
      FastSeverityCap = false
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
      FastSeverityCap =
        match setting "severityCap" with
        | None | Some "" -> false
        | Some "Fast" -> true
        | Some other -> invalidArg "lines" $"Unknown Guardrails severityCap '{other}'"
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

let relativeFile (config: Config) fileName =
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
    | Some(Policy.ExternalSource)
    | Some(Policy.InvalidSource _) -> true
    | _ -> false

let private tryRelative root path =
    try Some(Path.GetRelativePath(root, Path.GetFullPath path).Replace('\\', '/'))
    with _ -> None

/// Applies the enabled guardrail and legacy baseline policy. Tiered findings and note validation retain their
/// resolved severity and never generate baseline entries.
let resolvedFilePolicy (config: Config) fileName =
    match config.Policy, config.PolicyRoot, config.ProjectPath with
    | Some policy, Some root, Some project ->
        match classifyConfiguredSource config root fileName, tryRelative root project with
        | Policy.RepositorySource file, Some projectFile ->
            match Policy.resolve policy projectFile file with
            | Ok resolved -> Some resolved
            | Error error -> invalidArg "policy" error
        | Policy.ExternalSource, _
        | Policy.InvalidSource _, _
        | _, None -> None
    | _ -> None

let resolvedPolicy config (ctx: CliContext) = resolvedFilePolicy config ctx.FileName

/// Whether `[runtime].files` declares this source: the runtime implements Flow on top of raw tasks.
let isRuntimeFile (config: Config) fileName =
    match config.Policy, config.PolicyRoot with
    | Some policy, Some root ->
        (try Some(Path.GetFullPath fileName) with _ -> None)
        |> Option.exists (fun full -> policy.Runtime |> List.exists (fun file -> Path.GetFullPath(Path.Combine(root, file)) = full))
    | _ -> false

/// Raw-concurrency placement exemptions do not require typed facts; Fast and Full share them.
let exemptsRawConcurrency (config: Config) fileName =
    let role = resolvedFilePolicy config fileName |> Option.map _.Role
    let runtime = isRuntimeFile config fileName
    let host =
        match role, config.Policy, config.PolicyRoot, config.ProjectPath with
        | Some Policy.Host, Some policy, Some root, Some project ->
            let relative = Path.GetRelativePath(root, Path.GetFullPath project).Replace('\\', '/')
            policy.Exemptions.TryFind "AXG009" |> Option.defaultValue [] |> List.exists (fun pattern -> Policy.matchesGlob pattern relative)
        | _ -> false
    let test = role = Some Policy.Test && not (String.Equals(config.InTests, "all", StringComparison.OrdinalIgnoreCase))
    runtime || host || test || role = Some Policy.Capabilities

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

/// One CLI analyzer: the codes it reports and whether test projects run it by default.
type AnalyzerEntry = { Name: string; Codes: string list; RunsInTests: bool }

/// The single registry of CLI analyzers. A tracked name missing here fails closed instead of being skipped.
let analyzers =
    [ { Name = "EffectBoundary"; Codes = [ "AXG001" ]; RunsInTests = true }
      { Name = "SuppressionIntegrity"; Codes = [ "AXG002" ]; RunsInTests = true }
      { Name = "RaiseInFlow"; Codes = [ "AXG003" ]; RunsInTests = false }
      { Name = "Fixture"; Codes = [ "AXG004" ]; RunsInTests = true }
      { Name = "DiscardedCancellation"; Codes = [ "AXG005" ]; RunsInTests = false }
      { Name = "ReflectionFormatting"; Codes = [ "AXG006" ]; RunsInTests = false }
      { Name = "ResourceFromFiber"; Codes = [ "AXG007" ]; RunsInTests = false }
      { Name = "ScopeFromRun"; Codes = [ "AXG008" ]; RunsInTests = false }
      { Name = "RawConcurrency"; Codes = [ "AXG009" ]; RunsInTests = true }
      { Name = "SwallowedException"; Codes = [ "AXG010" ]; RunsInTests = false }
      { Name = "UncancellableWork"; Codes = [ "AXG011" ]; RunsInTests = false }
      { Name = "DiscardedWork"; Codes = [ "AXG012" ]; RunsInTests = false }
      { Name = "WaitForFacts"; Codes = [ "AXG013" ]; RunsInTests = true }
      { Name = "TaskSurface"; Codes = [ "AXG014" ]; RunsInTests = false } ]

let private testSetCodes = analyzers |> List.filter _.RunsInTests |> List.collect _.Codes |> Set.ofList

/// Whether a code is reported in a test project that uses the reduced analyzer set.
let reportsInTestSet code = Set.contains code testSetCodes

/// Selection happens before a rule touches symbols or typed trees; AXG002 still validates directives.
/// An unregistered analyzer runs wherever no selection is made (non-test roles, InTests=all); only the reduced
/// test set needs its codes, so there it fails closed instead of being silently skipped or run.
let shouldRunAnalyzer config rule =
    let selecting = usesTestAnalyzerSet config
    match analyzers |> List.tryFind (fun analyzer -> analyzer.Name = rule) with
    | Some analyzer -> Ok(analyzer.RunsInTests || not selecting)
    | None when not selecting -> Ok true
    | None -> Error $"analyzer {rule} is not registered in GuardrailConfig.analyzers, so the test analyzer set cannot select it"

/// Whether a project reports a code: all codes outside the reduced test set, otherwise only the test set's.
let runsInProject config code = not (usesTestAnalyzerSet config) || reportsInTestSet code

let private resolvableFullPath (config: Config) (fileName: string) =
    try
        let basePath =
            match sourceBaseDirectory config with
            | Some directory -> directory
            | None -> defaultArg config.PolicyRoot config.BaselineRoot
        Some(Path.GetFullPath(fileName, basePath))
    with _ -> None

/// Why a repository source cannot be checked as configured, as a sentence for warning AXGINF002, if it cannot. Hosts
/// record this in their build report so every build, including cache replays and reused build nodes, shows it. An
/// unusable source path means no rule checked the file; an unusable project path means the file was analyzed under the
/// default policy instead of its project's role. An external source is out of scope, not skipped.
let skipReason (config: Config) fileName =
    let skipped detail = Some $"source path is unusable{detail}, so no rule checked this source; fix the path or exclude the file"
    match sourceClassification config fileName with
    | Some(Policy.InvalidSource explanation) -> skipped $": {explanation}"
    | Some Policy.ExternalSource -> None
    | _ when (resolvableFullPath config fileName).IsNone -> skipped ""
    | _ ->
        match config.Policy, config.PolicyRoot, config.ProjectPath with
        | Some _, Some root, Some project when (tryRelative root project).IsNone ->
            Some "project path is unusable, so this source was analyzed under the default policy rather than its project's role; fix the project path"
        | _ -> None

let isBoundaryFile (config: Config) fileName =
    config.BoundaryFiles.Contains "*"
    || (resolvableFullPath config fileName |> Option.exists config.BoundaryFiles.Contains)

let applyWith (config: Config) (guardrail: string) (ctx: CliContext) (messages: Message list) : Message list =
    if isSkippedSource config ctx.FileName then [] else
    let sourcePath = resolvableFullPath config ctx.FileName
    if sourcePath.IsNone then [] else
    let policy = resolvedPolicy config ctx
    let level = policy |> Option.bind (fun resolved -> resolved.Rules.TryFind "effects.ambient") |> Option.defaultValue Policy.Deny
    if not (config.EnabledGuardrails.Contains guardrail) then
        []
    else
        let remaining = Collections.Generic.Dictionary<string * string * string, int>()
        for entry in config.Baseline do
            remaining[(entry.RuleId, entry.File, entry.Fingerprint)] <- entry.Count

        let findings =
            messages
            |> List.filter (fun message -> runsInProject config message.Code)
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
            let tiered = Set.contains message.Code (set [ "AXG009"; "AXG010"; "AXG011"; "AXG012"; "AXG013"; "AXG014" ]) || message.Type = "Axial Note Integrity"
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

/// Fast never reports a finding as an Error, so the capped fallback turns Error findings into warnings.
let capSeverity (config: Config) (messages: Message list) =
    if config.FastSeverityCap then
        messages |> List.map (fun message -> if message.Severity = Severity.Error then { message with Severity = Severity.Warning } else message)
    else messages

let apply (guardrail: string) (ctx: CliContext) (messages: Message list) = applyWith (load ()) guardrail ctx messages

let applyCore (ctx: CliContext) (messages: Message list) = apply CoreGuardrail ctx messages
