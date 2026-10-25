namespace Axial.Tool

open System
open System.IO
open System.Text.Json
open Axial.Tool.PolicyLock

module Program =
    let private workflow = """Axial policy workflow:
  1. axial init
  2. axial check
  3. If policy changed, ask a human to review and run axial policy approve.

The lock is a workflow guard, not a security boundary. Direct lock edits remain visible in Git; phase 3 agent hooks will block them.
HUMAN DECISION — agents must not run policy approval commands; ask the user."""
    let private policyWarning = "HUMAN DECISION — agents must not run this; ask the user."
    let private json value = JsonSerializer.Serialize(value,JsonSerializerOptions(WriteIndented=true))
    let mutable private commandName = "help"
    let private jsonChanges (changes: ChangeSummary option) =
        let c=defaultArg changes {RulesDowngraded=[];BaselineAdded=[];SuppressionsAdded=[];BoundariesAdded=[];ProtectedChanged=[];TestsRemoved=[]}
        {| rulesDowngraded=c.RulesDowngraded; baselineAdded=c.BaselineAdded; suppressionsAdded=c.SuppressionsAdded
           boundariesAdded=c.BoundariesAdded; protectedChanged=c.ProtectedChanged; testsRemoved=c.TestsRemoved |}
    let private emitWith (changes: ChangeSummary option) asJson ok (message: string) =
        let status = if ok then "ok" else "error"
        let errorCode : string =
            if ok then null
            elif message.Contains("Usage:") || message.StartsWith("Unknown command") then "AXIAL_USAGE"
            elif message.Contains("baseline growth refused") then "AXIAL_BASELINE_GROWTH"
            elif message.Contains("test/assertion count decreased") then "AXIAL_TESTS_REMOVED"
            elif message.Contains("regenerate the lock") || message.Contains("lock") && message.Contains("not found") then "AXIAL_MALFORMED_LOCK"
            elif message.Contains("Refusing to overwrite") then "AXIAL_INIT_EXISTS"
            elif message.Contains("Approval cancelled") then "AXIAL_CANCELLED"
            elif changes |> Option.exists(isEmpty >> not) then "AXIAL_POLICY_CHANGED"
            else "AXIAL_MALFORMED_CONFIG"
        let categorized = jsonChanges changes
        if asJson then printfn "%s" (json {| schema="axial.policy.v1"; command=commandName; status=status; errorCode=errorCode; changes=categorized; message=message |})
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
            else emitWith (Some changes) asJson false (formatSummary changes+"\nThe lock is a workflow guard, not a security boundary; direct edits remain visible in Git.")

    let private confirm () =
        printf "Type 'approve' to record this reviewed policy change: "
        String.Equals(Console.ReadLine(),"approve",StringComparison.Ordinal)

    let private approve asJson nonInteractive acceptRemoved baselineRule =
        match inspect(root()), parseLock(Path.Combine(root(),LockName)) with
        | Error e, _ | _, Error e -> emit asJson false e
        | Ok current, Ok locked ->
            let changes=diff locked current
            if isEmpty changes then emit asJson true "No policy changes to approve."
            else
                let approval = match baselineRule with Some r->BaselineRule r | None when acceptRemoved->AcceptRemovedTests | None->Normal
                match validateApproval approval locked current changes with
                | Error e -> emitWith (Some changes) asJson false (formatSummary changes+Environment.NewLine+e)
                | Ok () ->
                    let summary=formatSummary changes
                    if not asJson then printfn "%s\n%s" policyWarning summary
                    if nonInteractive || (not asJson && confirm()) then
                        writeAtomic (Path.Combine(root(),LockName)) (serialize current)
                        emitWith (Some changes) asJson true (summary+Environment.NewLine+"Policy lock updated after human review.")
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
        | ["policy";"approve";"--help"] | ["policy";"baseline";"--help"] -> emit asJson true (policyWarning+"\n"+workflow)
        | "policy"::"approve"::rest ->
            if rest |> List.exists(fun x->x<>"--yes-i-reviewed" && x<>"--accept-removed-tests") then emit asJson false (policyWarning+"\nUsage: axial policy approve [--accept-removed-tests] [--yes-i-reviewed] [--json]")
            else approve asJson (List.contains "--yes-i-reviewed" rest) (List.contains "--accept-removed-tests" rest) None
        | ["policy";"baseline";"--rule";rule] -> approve asJson false false (Some rule)
        | ["policy";"baseline";"--rule";rule;"--yes-i-reviewed"] -> approve asJson true false (Some rule)
        | ["--help"] | ["help"] -> help asJson
        | _ -> emit asJson false ("Unknown command.\n"+workflow)
