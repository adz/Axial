namespace Axial.Tool

open System
open System.IO
open System.Text.Encodings.Web
open Axial.Tool.PolicyLock

module Program =
    let private workflow = """Axial policy workflow:
  1. axial init
  2. axial check
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
    let private emitWith (changes: ChangeSummary option) asJson ok (message: string) =
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
            Console.WriteLine("{\"schema\":\"axial.policy.v1\",\"command\":" + jsonString commandName + ",\"status\":" + jsonString status + ",\"errorCode\":" + errorJson + ",\"changes\":{" + fields + "},\"message\":" + jsonString message + "}")
        else (if ok then printfn "%s" message else eprintfn "%s" message)
        if ok then 0 else 1
    let private emit asJson ok message = emitWith None asJson ok message
    let private root () = Directory.GetCurrentDirectory()

    let private check asJson =
        match inspect(root()), parseLock(Path.Combine(root(),LockName)) with
        | Error e, _ | _, Error e -> emit asJson false e
        | Ok current, Ok locked ->
            let changes=diff locked current
            if isEmpty changes then emit asJson true ("Axial policy check passed.\nThe lock is tamper-evident, not a security boundary. Enforcement is the repository's choice.")
            else
                let count = changes.RulesDowngraded.Length + changes.BaselineAdded.Length + changes.SuppressionsAdded.Length + changes.BoundariesAdded.Length + (changes.ProtectedChanged |> List.filter ((<>) LockName) |> List.length)
                emitWith (Some changes) asJson true $"WARNING: Policy changed since the last review: {max 1 count} changes. Run `axial policy review`."

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

    let private defaultConfig = "version = 2\nbaseline = \"Axial.Guardrails.baseline\"\n\n[default]\nrole = \"domain\"\n"
    let private baselineHeader = "# Axial.Guardrails baseline: rule<TAB>file<TAB>fingerprint[ x<count>]\n"

    let private init asJson =
        let repo = root()
        let config = Path.Combine(repo, ConfigName)
        let lock = Path.Combine(repo, LockName)
        let configExists = File.Exists config
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
                emit asJson ok (message + Environment.NewLine + $"created: {createdText}; reused: {reusedText}")
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
                        let info = Diagnostics.ProcessStartInfo("dotnet")
                        info.WorkingDirectory <- repo
                        info.UseShellExecute <- false
                        info.RedirectStandardOutput <- true
                        info.RedirectStandardError <- true
                        for arg in ["build"; project; "--nologo"; "-v:q"; "-m:1"; "-nodeReuse:false";
                                    "-p:_AxialGuardrailsBaselineProposalDirectory=" + proposal;
                                    "-p:_AxialGuardrailsBaselineRuleCode=" + diagnostic] do
                            info.ArgumentList.Add arg
                        use child = Diagnostics.Process.Start info
                        let output = child.StandardOutput.ReadToEndAsync()
                        let errors = child.StandardError.ReadToEndAsync()
                        if not (child.WaitForExit(180000)) then
                            child.Kill(true)
                            child.WaitForExit()
                            failwith $"Baseline generation timed out for {project}."
                        if child.ExitCode <> 0 then
                            failwith ($"Baseline generation failed for {project}; resolve compilation, tool or unrelated diagnostics first.\n" + output.Result + errors.Result)
                    let outputs = Directory.GetFiles(proposal, "*.baseline", SearchOption.AllDirectories)
                    if outputs.Length = 0 then failwith "No projects ran Guardrails candidate generation. Restore/update Axial.Guardrails and enable analysis."
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
        | ["check"] -> check asJson
        | ["init"] -> init asJson
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
                    if checkOnly && not result.Loosened.IsEmpty then 1 else 0
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
