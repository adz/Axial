namespace Axial.Tool

open System
open System.IO
open System.Text.Encodings.Web
open Axial.Tool.PolicyLock

module Program =
    let private workflow = """Axial policy workflow:
  1. axial init [--ci github|gitlab|bitbucket|other]
  2. axial check [--full [--configuration <name>]]
     Without --full: compares policy with the lock (drift warns, never fails) and prints a per-project raw-Task
     trend (lexical counts; --json field rawTask). Builds run `check --build`: one drift line per build, no trend.
     --full checks policy review first, then builds every solution project/TFM in Full mode
     (Release by default), requires complete coverage and rejects Error findings.
     Never writes a lock or baseline.
  3. axial policy review [--since <ref>] [--check] [--json]
  4. A human reads the review, then runs axial policy approve [--yes-i-reviewed].

The lock is tamper-evident, not a security boundary. Enforcement is the repository's choice.
Approval means a human read the review. The first review requires --since <ref>."""
    let private policyWarning = "HUMAN DECISION — agents must not run this; ask the user."
    let private jsonString (value: string) = "\"" + JavaScriptEncoder.Default.Encode(value) + "\""
    let private jsonArray values = values |> List.map jsonString |> String.concat "," |> fun content -> "[" + content + "]"
    let private jsonBaseline values =
        values |> List.map(fun entry -> "{\"rule\":" + jsonString entry.Rule + ",\"file\":" + jsonString entry.File + ",\"fingerprint\":" + jsonString entry.Fingerprint + ",\"count\":" + string entry.Count + "}") |> String.concat "," |> fun content -> "[" + content + "]"
    let mutable private commandName = "help"
    let private jsonChanges (changes: ChangeSummary option) =
        let c=defaultArg changes {RulesDowngraded=[];BaselineAdded=[];SuppressionsAdded=[];BoundariesAdded=[];ProtectedChanged=[]}
        {| rulesDowngraded=c.RulesDowngraded; baselineAdded=c.BaselineAdded; suppressionsAdded=c.SuppressionsAdded
           boundariesAdded=c.BoundariesAdded; protectedChanged=c.ProtectedChanged |}
    let private emitWithExtra (extra: (string * string) list) (changes: ChangeSummary option) asJson ok (message: string) =
        let status = if ok then "ok" else "error"
        let errorCode : string =
            if ok then null
            elif message.Contains("Usage:") || message.StartsWith("Unknown command") then "AXIAL_USAGE"
            elif message.Contains("baseline growth refused") then "AXIAL_BASELINE_GROWTH"
            elif message.Contains("regenerate the lock") || message.Contains("lock") && message.Contains("not found") then "AXIAL_MALFORMED_LOCK"
            elif message.Contains("Refusing to overwrite") then "AXIAL_INIT_EXISTS"
            elif message.Contains("Approval cancelled") then "AXIAL_CANCELLED"
            elif changes |> Option.exists(isEmpty >> not) then "AXIAL_POLICY_CHANGED"
            else "AXIAL_MALFORMED_CONFIG"
        let categorized = jsonChanges changes
        if asJson then
            let errorJson = if isNull errorCode then "null" else jsonString errorCode
            let fields =
                [ "\"rulesDowngraded\":" + jsonArray categorized.rulesDowngraded
                  "\"baselineAdded\":" + jsonBaseline categorized.baselineAdded
                  "\"suppressionsAdded\":" + jsonArray categorized.suppressionsAdded
                  "\"boundariesAdded\":" + jsonArray categorized.boundariesAdded
                  "\"protectedChanged\":" + jsonArray categorized.protectedChanged ] |> String.concat ","
            let extraFields = extra |> List.map (fun (name, value) -> "," + jsonString name + ":" + value) |> String.concat ""
            Console.WriteLine("{\"schema\":\"axial.policy.v1\",\"command\":" + jsonString commandName + ",\"status\":" + jsonString status + ",\"errorCode\":" + errorJson + ",\"changes\":{" + fields + "},\"message\":" + jsonString message + extraFields + "}")
        else (if ok then printfn "%s" message else eprintfn "%s" message)
        if ok then 0 else 1
    let private emitWith changes asJson ok message = emitWithExtra [] changes asJson ok message
    let private emit asJson ok message = emitWith None asJson ok message
    let private root () = Directory.GetCurrentDirectory()

    /// Every project/TFM of a build runs `check --build`; the drift notice is claimed once per drift state, so one
    /// build prints at most one line. A claim older than the window lets a later build show it again.
    let private driftNoticeDue (summary: string) =
        try
            let digest (text: string) = Security.Cryptography.SHA256.HashData(Text.Encoding.UTF8.GetBytes text) |> Convert.ToHexString
            let directory = Path.Combine(Path.GetTempPath(), "axial-guardrails-drift")
            Directory.CreateDirectory directory |> ignore
            let marker = Path.Combine(directory, digest (Path.GetFullPath(root())) + "-" + digest summary)
            let claim () =
                try
                    (new FileStream(marker, FileMode.CreateNew)).Dispose()
                    true
                with :? IOException -> false
            if claim () then true
            elif DateTime.UtcNow - File.GetLastWriteTimeUtc marker > TimeSpan.FromMinutes 30.0 then
                try File.Delete marker with :? IOException -> ()
                claim ()
            else false
        // Never let deduplication hide drift.
        with :? IOException | :? UnauthorizedAccessException -> true

    let private rawTaskSummary () =
        let repo = root()
        let policy =
            try Axial.Guardrails.Policy.parse repo knownRules (File.ReadAllText(Path.Combine(repo, ConfigName))) |> Result.toOption
            with _ -> None
        RawTaskCount.count repo (repositoryFiles repo) policy

    let private rawTaskJson (counts: RawTaskCount.ProjectCount list) =
        counts
        |> List.map (fun count ->
            "{\"project\":" + jsonString count.Project + ",\"role\":" + jsonString count.Role
            + ",\"taskExpressions\":" + string count.TaskExpressions + ",\"taskSignatures\":" + string count.TaskSignatures + "}")
        |> String.concat "," |> fun content -> "[" + content + "]"

    /// `build` is the per-project build invocation: no trend summary, and at most one drift line per build.
    let private check asJson build =
        match inspect(root()), parseLock(Path.Combine(root(),LockName)) with
        | Error e, _ | _, Error e -> emit asJson false e
        | Ok current, Ok locked ->
            let changes=diff locked current
            let counts = if build then [] else rawTaskSummary ()
            let extra = if build then [] else [ "rawTask", rawTaskJson counts ]
            let withTrend message = if build || asJson then message else message + "\n" + RawTaskCount.format counts
            if isEmpty changes then emitWithExtra extra None asJson true (withTrend "Axial policy check passed.\nThe lock is tamper-evident, not a security boundary. Enforcement is the repository's choice.")
            elif build && not (driftNoticeDue (formatSummary changes)) then
                emitWithExtra extra (Some changes) asJson true "Axial policy check: the policy-drift notice was already shown for this change."
            else
                let count = changes.RulesDowngraded.Length + changes.BaselineAdded.Length + changes.SuppressionsAdded.Length + changes.BoundariesAdded.Length + (changes.ProtectedChanged |> List.filter ((<>) LockName) |> List.length)
                emitWithExtra extra (Some changes) asJson true (withTrend $"WARNING: Policy changed since the last review: {max 1 count} changes. Run `axial policy review`.")

    let private fullCheck asJson configuration =
        let directory = Path.Combine(Path.GetTempPath(), "axial-full-check-" + Guid.NewGuid().ToString("N"))
        try
            try
                let solutions =
                    Directory.GetFiles(root(), "*.slnx")
                    |> Array.append (Directory.GetFiles(root(), "*.sln"))
                if solutions.Length <> 1 then
                    emit asJson false "axial check --full requires exactly one solution (.slnx or .sln) in the current directory."
                else
                    match PolicyReview.review (root()) None with
                    | Error error -> emit asJson false ("policy review --check failed: " + error)
                    | Ok review when not (PolicyReview.checkPassed review) ->
                        emit asJson false ("policy review --check failed:\n" + PolicyReview.format review)
                    | Ok review ->
                        // A per-run import path invalidates MSBuildAllProjects and defeats exact cache reuse.
                        let expectedTarget = Path.Combine(AppContext.BaseDirectory, "FullCheck.targets")
                        if not (File.Exists expectedTarget) then
                            emit asJson false ("Full analysis coverage collector is missing: " + expectedTarget + ". Reinstall Axial.Tool.")
                        else
                            Directory.CreateDirectory directory |> ignore
                            let code, _, _ = Command.run (root()) "dotnet"
                                                [ "build"; solutions[0]; "--configuration"; configuration; "--nologo"; "-v"; "minimal"; "-nodeReuse:false"
                                                  "-p:AxialGuardrailsMode=Full"; "-p:AxialGuardrailsCheckpoint=true"
                                                  "-p:_AxialGuardrailsBaselineProposalDirectory="; "-p:_AxialGuardrailsBaselineRuleCode="
                                                  "-p:CustomAfterMicrosoftCommonTargets=" + expectedTarget
                                                  "-p:_AxialGuardrailsFullCheckDirectory=" + directory ] 1800000 true
                            if code <> 0 then emit asJson false "Full analysis failed (findings, build failure, or incomplete coverage); see build diagnostics."
                            else
                                let reports = Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories)
                                let expected = Directory.GetFiles(directory, "*.expected") |> Array.map (File.ReadAllText >> _.Trim()) |> Set.ofArray
                                let incomplete = reports |> Array.tryFind (Coverage.complete (Some "Full") >> not)
                                match incomplete with
                                | Some path -> emit asJson false ("Full analysis coverage is incomplete: " + path)
                                | None ->
                                    let actual = reports |> Array.map Coverage.identity |> Set.ofArray
                                    if expected.IsEmpty || actual <> expected || reports.Length <> actual.Count then
                                        emit asJson false ("Full analysis coverage is incomplete: expected project/TFM records do not match exported records. Missing: " + String.concat ", " (Set.difference expected actual))
                                    elif reports |> Array.exists Coverage.errors then
                                        emit asJson false "Full analysis reported Error findings."
                                    else emit asJson true ("Full analysis passed for " + string reports.Length + " project/TFM records.\n" + PolicyReview.format review)
            with error -> emit asJson false ("Full analysis failed: " + error.Message)
        finally
            try if Directory.Exists directory then Directory.Delete(directory, true)
            with :? IOException | :? UnauthorizedAccessException -> ()

    let private confirm () =
        printf "Type 'approve' to record this reviewed policy change: "
        String.Equals(Console.ReadLine(),"approve",StringComparison.Ordinal)

    let private approve asJson nonInteractive baselineRule =
        match inspect(root()), parseLock(Path.Combine(root(),LockName)) with
        | Error e, _ | _, Error e -> emit asJson false e
        | Ok current, Ok locked ->
            let changes=diff locked current
            let approval = match baselineRule with Some r->BaselineRule r | None->Normal
            match validateApproval approval locked current changes with
            | Error e -> emitWith (Some changes) asJson false ((if asJson then formatJsonSummary changes else formatSummary changes)+Environment.NewLine+e)
            | Ok () ->
                let summary=if asJson then formatJsonSummary changes else formatSummary changes
                if not asJson then printfn "%s\n%s" policyWarning summary
                if nonInteractive || (not asJson && confirm()) then
                    try
                        writeAtomic (Path.Combine(root(),LockName)) (PolicyReview.prepareReviewedLock (root()) current)
                        emitWith (Some changes) asJson true (summary+Environment.NewLine+"Policy lock updated: a human read the review.")
                    with error -> emit asJson false error.Message
                else emit asJson false "Approval cancelled. Use --yes-i-reviewed for reviewed non-interactive changes."

    let private ciHosts = ["github"; "gitlab"; "bitbucket"; "other"]

    let private defaultConfig ci =
        let automation =
            match ci with
            | "github" -> [".github/workflows/**"]
            | "gitlab" -> [".gitlab-ci.yml"; ".gitlab/ci/**"]
            | "bitbucket" -> ["bitbucket-pipelines.yml"]
            | _ -> []
        "version = 2\n" +
        "# Baseline records reviewed existing findings, not new exceptions.\n" +
        "baseline = \"Axial.Guardrails.baseline\"\n\n" +
        "# Roles describe project authority; unmatched projects use the default role.\n" +
        "[default]\nrole = \"domain\"\n\n" +
        "# Assign repository-relative project globs under [projects], for example:\n" +
        "# [projects]\n# test = [\"tests/**/*.fsproj\"]\n" +
        "# Test role selects Fixture, WaitForFacts, RawConcurrency and SuppressionIntegrity.\n" +
        "# AxialGuardrailsInTests=all enables the full analyzer set for test-role projects.\n\n" +
        "[review]\n# Automation files enforce policy; globs are relative to the repository root.\n" +
        "# Adding a verification script here lists its changes in axial policy review.\n" +
        "# An absent or empty list disables automation tracking.\n" +
        "automation = [" + (automation |> List.map (fun path -> "\"" + path + "\"") |> String.concat ", ") + "]\n"
    let private baselineHeader = "# Axial.Guardrails baseline: rule<TAB>file<TAB>fingerprint[ x<count>]\n"

    let rec private promptCi () =
        printf "CI host (GitHub, GitLab, Bitbucket, Other) [Other]: "
        let answer = Console.ReadLine()
        let host = if String.IsNullOrWhiteSpace answer then "other" else answer.Trim().ToLowerInvariant()
        if List.contains host ciHosts then host
        else
            printfn "Choose GitHub, GitLab, Bitbucket, or Other."
            promptCi ()

    let private init asJson selectedCi =
        let repo = root()
        let config = Path.Combine(repo, ConfigName)
        let lock = Path.Combine(repo, LockName)
        let configExists = File.Exists config
        let ci, notice =
            match selectedCi with
            | Some host -> host, ""
            | None when configExists -> "other", ""
            | None when not asJson && not Console.IsInputRedirected -> promptCi (), ""
            | None -> "other", "Non-interactive init: using Other CI (automation = []). Use --ci to select a host.\n"
        let defaultConfig = defaultConfig ci
        let plan =
            if not (Axial.Guardrails.Policy.isRepositorySource repo config) then
                Error $"{ConfigName} resolves outside the repository root"
            elif configExists then
                match Axial.Guardrails.Policy.parse repo knownRules (File.ReadAllText config) with
                | Ok parsed -> Ok(defaultArg parsed.Baseline DefaultBaseline, None)
                | Error errors -> Error(String.concat Environment.NewLine errors)
            else
                match Axial.Guardrails.Policy.parse repo knownRules defaultConfig with
                | Ok parsed -> Ok(defaultArg parsed.Baseline DefaultBaseline, Some defaultConfig)
                | Error errors -> Error(String.concat Environment.NewLine errors)
        match plan with
        | Error error -> emit asJson false error
        | Ok (baselineName, newConfig) ->
            let created = ResizeArray<string>()
            let reused = ResizeArray<string>()
            let finish ok message =
                let createdText = if created.Count = 0 then "nothing" else String.concat ", " created
                let reusedText = if reused.Count = 0 then "nothing" else String.concat ", " reused
                emit asJson ok (notice + message + Environment.NewLine + $"created: {createdText}; reused: {reusedText}")
            try
                match newConfig with
                | Some text when not configExists ->
                    match writeNew config text with
                    | Ok () -> created.Add ConfigName
                    | Error error -> raise (IOException error)
                | _ -> reused.Add ConfigName
                let baseline = Path.Combine(repo, baselineName.Replace('/', Path.DirectorySeparatorChar))
                if not (Axial.Guardrails.Policy.isRepositorySource repo baseline) then
                    raise (IOException $"Configured baseline '{baselineName}' resolves outside the repository root.")
                let baselineDirectory = Path.GetDirectoryName baseline
                if not (String.IsNullOrEmpty baselineDirectory) && not (Directory.Exists baselineDirectory) then
                    Directory.CreateDirectory baselineDirectory |> ignore
                if not (File.Exists baseline) then
                    match writeNew baseline baselineHeader with
                    | Ok () -> created.Add baselineName
                    | Error error -> raise (IOException error)
                else reused.Add baselineName
                if File.Exists lock then
                    reused.Add LockName
                    finish true "Policy bootstrap is already complete. A person must still review and approve later policy changes."
                else
                    match inspect repo with
                    | Error error -> finish false ("Policy artifacts were created only as far as reported. " + error)
                    | Ok snapshot ->
                        match writeNew lock (serialize snapshot) with
                        | Ok () ->
                            created.Add LockName
                            finish true "Policy bootstrap created the missing artifacts. A person must review subsequent policy drift and run 'axial policy approve'."
                        | Error error -> finish false ("The lock was not created. " + error)
            with error -> finish false ("Policy bootstrap stopped: " + error.Message)

    let private baselineHelp = "Usage: axial policy baseline --rule <id> [--json]\nPrepare candidate baseline entries for current findings (effects.ambient or AXG001–008). Never records approval or changes the lock. A human reviews the entries, then runs axial policy approve --baseline-rule <id>."

    let private baseline asJson rule =
        match baselineDiagnostic rule, inspect(root()) with
        | None, _ -> emit asJson false ("Unsupported baseline rule.\n" + baselineHelp)
        | _, Error error -> emit asJson false error
        | Some diagnostic, Ok snapshot ->
            let repo = root()
            let proposal = Path.Combine(repo, "artifacts", "axial-baseline-" + Guid.NewGuid().ToString("N"))
            try
                try
                    Directory.CreateDirectory proposal |> ignore
                    let projects = repositoryFiles repo |> Array.filter (fun path -> path.EndsWith(".fsproj", StringComparison.Ordinal) && Axial.Guardrails.Policy.isRepositorySource repo path)
                    if projects.Length = 0 then failwith "No F# projects found for baseline generation."
                    for project in projects do
                        let code, output, errors = Command.run repo "dotnet"
                                                    ["build"; project; "--nologo"; "-v:q"; "-m:1"; "-nodeReuse:false"; "-p:AxialGuardrailsMode=Full";
                                                     "-p:_AxialGuardrailsBaselineProposalDirectory=" + proposal;
                                                     "-p:_AxialGuardrailsBaselineRuleCode=" + diagnostic] 180000 false
                        let incomplete =
                            Directory.GetFiles(proposal, "*.coverage.xml", SearchOption.AllDirectories)
                            |> Array.exists (Coverage.complete None >> not)
                        if code <> 0 || incomplete then
                            failwith ($"Baseline generation failed for {project}; resolve compilation, tool or unrelated diagnostics first.\n" + output + errors)
                    let outputs = Directory.GetFiles(proposal, "*.baseline", SearchOption.AllDirectories)
                    if outputs.Length = 0 then failwith "No projects ran Guardrails candidate generation. Restore/update Axial.Guardrails and enable analysis."
                    for path in outputs do
                        if not (File.Exists(path + ".coverage.xml")) then
                            failwith "Baseline generation has no complete coverage receipt. Restore/update Axial.Guardrails and rerun."
                    let candidates = outputs |> Array.toList |> List.collect baselineEntries |> List.filter (fun entry -> entry.Rule = diagnostic)
                    let baselinePath = Path.Combine(repo, snapshot.BaselinePath)
                    let merged =
                        snapshot.Baseline @ candidates
                        |> List.groupBy (fun entry -> entry.Rule, entry.File, entry.Fingerprint)
                        // Linked sources, project references and target frameworks must not multiply counts.
                        |> List.map (fun (_, entries) -> entries |> List.maxBy _.Count)
                        |> List.sortBy (fun entry -> entry.Rule, entry.File, entry.Fingerprint)
                    let text = baselineHeader + (merged |> List.map (fun entry ->
                        $"{entry.Rule}\t{entry.File}\t{entry.Fingerprint}" + (if entry.Count > 1 then $" x{entry.Count}" else "")) |> String.concat "\n") + (if merged.IsEmpty then "" else "\n")
                    writeAtomic baselinePath text
                    emit asJson true $"Prepared {candidates |> List.distinct |> List.length} candidate entries for {rule} in {snapshot.BaselinePath}. The lock is unchanged. A human must review, then run 'axial policy approve --baseline-rule {rule}'."
                with error -> emit asJson false error.Message
            finally
                if Directory.Exists proposal then Directory.Delete(proposal, true)

    let private help asJson = emit asJson true (workflow + "\n\n" + baselineHelp)

    let private removedSeverity () =
        let value = Environment.GetEnvironmentVariable "AXIAL_GUARDRAILS_SEVERITY"
        let propertyStyle = Environment.GetEnvironmentVariable "AxialGuardrailsSeverity"
        not (String.IsNullOrWhiteSpace value) || not (String.IsNullOrWhiteSpace propertyStyle)

    let private run argv =
        let args=argv|>Array.filter((<>)"--json")|>Array.toList
        let asJson=argv|>Array.contains "--json"
        commandName <- if args.IsEmpty then "help" else String.concat " " (args |> List.take (min 2 args.Length))
        match args with
        | [] -> help asJson
        | ["check"] -> check asJson false
        | ["check"; "--build"] -> check asJson true
        | ["check"; "--full"] -> fullCheck asJson "Release"
        | ["check"; "--full"; "--configuration"; configuration] when not (String.IsNullOrWhiteSpace configuration) -> fullCheck asJson configuration
        | ["init"] -> init asJson None
        | ["init"; "--ci"; host] when List.contains host ciHosts -> init asJson (Some host)
        | "init"::_ -> emit asJson false "Usage: axial init [--ci github|gitlab|bitbucket|other] [--json]"
        | "policy"::"review"::rest ->
            let rec options (since: string option) checkOnly = function
                | [] -> Ok (since, checkOnly)
                | "--since"::point::tail when since.IsNone -> options (Some point) checkOnly tail
                | "--check"::tail when not checkOnly -> options since true tail
                | _ -> Error "Usage: axial policy review [--since <ref>] [--check] [--json]"
            match options None false rest with
            | Error error -> emit asJson false error
            | Ok (since, checkOnly) ->
                match PolicyReview.review (root()) since with
                | Error error -> emit asJson false error
                | Ok result ->
                    if asJson then
                        printfn "%s" (System.Text.Json.JsonSerializer.Serialize {| schema = "axial.policy.review.v1"; since = result.Since; loosened = result.Loosened; removed = result.Removed |})
                    else printfn "%s" (PolicyReview.format result)
                    if checkOnly && not (PolicyReview.checkPassed result) then 1 else 0
        | ["policy";"baseline";"--help"] -> emit asJson true baselineHelp
        | ["policy";"approve";"--help"] -> emit asJson true (policyWarning+"\nUsage: axial policy approve [--baseline-rule <id>] [--yes-i-reviewed] [--json]\n"+workflow)
        | "policy"::"approve"::rest ->
            let rec options selected reviewed = function
                | [] -> approve asJson reviewed selected
                | "--yes-i-reviewed"::tail when not reviewed -> options selected true tail
                | "--baseline-rule"::rule::tail when selected.IsNone && (baselineDiagnostic rule).IsSome -> options (Some rule) reviewed tail
                | _ -> emit asJson false (policyWarning+"\nUsage: axial policy approve [--baseline-rule <id>] [--yes-i-reviewed] [--json]")
            options None false rest
        | ["policy";"baseline";"--rule";rule] -> baseline asJson rule
        | ["--help"] | ["help"] -> help asJson
        | _ -> emit asJson false ("Unknown command.\n"+workflow)

    [<EntryPoint>]
    let main argv =
        if removedSeverity () then
            let message = "error AXIALGR001: AxialGuardrailsSeverity / AXIAL_GUARDRAILS_SEVERITY is removed in Axial 0.12. Remove it and configure supported per-role rule levels in Axial.Guardrails.toml. See the policy-roles migration row in the 0.12.0 release notes (dev-docs/releases/0.12.0.md#policy-roles). AXG009-013 retain their fixed block/guide policy; AxialGuardrailsCheckpoint controls unacknowledged guides at checkpoints."
            eprintfn "%s" message
            if Array.contains "--json" argv then emit true false message else 1
        else run argv
