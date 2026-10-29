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
            if isEmpty changes then emit asJson true ("Axial policy check passed.\nThe lock is a workflow guard, not a security boundary; direct edits remain visible in Git.")
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

    let private init asJson =
        let config=Path.Combine(root(),ConfigName)
        let baseline=Path.Combine(root(),DefaultBaseline)
        let lock=Path.Combine(root(),LockName)
        if File.Exists config || File.Exists lock then emit asJson false "Refusing to overwrite existing Guardrails policy files."
        else
            File.WriteAllText(config,"version = 2\nbaseline = \"Axial.Guardrails.baseline\"\n\n[default]\nrole = \"domain\"\n")
            if not(File.Exists baseline) then File.WriteAllText(baseline,"# Axial.Guardrails baseline: rule<TAB>file<TAB>fingerprint[ x<count>]\n")
            match inspect(root()) with
            | Error e -> emit asJson false e
            | Ok snapshot -> writeAtomic lock (serialize snapshot); emit asJson true "Created Axial.Guardrails.toml, Axial.Guardrails.baseline, and Axial.Guardrails.lock."

    let private help asJson = emit asJson true workflow

    [<EntryPoint>]
    let main argv =
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
        | ["policy";"approve";"--help"] | ["policy";"baseline";"--help"] -> emit asJson true (policyWarning+"\n"+workflow)
        | "policy"::"approve"::rest ->
            if rest |> List.exists((<>) "--yes-i-reviewed") then emit asJson false (policyWarning+"\nUsage: axial policy approve [--yes-i-reviewed] [--json]")
            else approve asJson (List.contains "--yes-i-reviewed" rest) None
        | ["policy";"baseline";"--rule";rule] -> approve asJson false (Some rule)
        | ["policy";"baseline";"--rule";rule;"--yes-i-reviewed"] -> approve asJson true (Some rule)
        | ["--help"] | ["help"] -> help asJson
        | _ -> emit asJson false ("Unknown command.\n"+workflow)
