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

HUMAN DECISION — agents must not run policy approval commands; ask the user."""
    let private policyWarning = "HUMAN DECISION — agents must not run this; ask the user."
    let private json value = JsonSerializer.Serialize(value,JsonSerializerOptions(WriteIndented=true))
    let private emit asJson ok message =
        if asJson then printfn "%s" (json {| ok=ok; message=message |}) else (if ok then printfn "%s" message else eprintfn "%s" message)
        if ok then 0 else 1
    let private root () = Directory.GetCurrentDirectory()

    let private check asJson =
        match inspect(root()), parseLock(Path.Combine(root(),LockName)) with
        | Error e, _ | _, Error e -> emit asJson false e
        | Ok current, Ok locked ->
            let changes=diff locked current
            if isEmpty changes then emit asJson true "Axial policy check passed."
            else emit asJson false (formatSummary changes)

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
                | Error e -> emit asJson false (formatSummary changes+Environment.NewLine+e)
                | Ok () ->
                    let summary=formatSummary changes
                    if not asJson then printfn "%s\n%s" policyWarning summary
                    if nonInteractive || (not asJson && confirm()) then
                        writeAtomic (Path.Combine(root(),LockName)) (serialize current)
                        emit asJson true (summary+Environment.NewLine+"Policy lock updated after human review.")
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

    let private help () = printfn "%s" workflow; 0

    [<EntryPoint>]
    let main argv =
        let args=argv|>Array.filter((<>)"--json")|>Array.toList
        let asJson=argv|>Array.contains "--json"
        match args with
        | [] -> if asJson then emit true true workflow else help()
        | ["check"] -> check asJson
        | ["init"] -> init asJson
        | "policy"::"approve"::rest ->
            if rest |> List.exists(fun x->x<>"--yes-i-reviewed" && x<>"--accept-removed-tests") then emit asJson false (policyWarning+"\nUsage: axial policy approve [--accept-removed-tests] [--yes-i-reviewed] [--json]")
            else approve asJson (List.contains "--yes-i-reviewed" rest) (List.contains "--accept-removed-tests" rest) None
        | ["policy";"baseline";"--rule";rule] -> approve asJson false false (Some rule)
        | ["policy";"baseline";"--rule";rule;"--yes-i-reviewed"] -> approve asJson true false (Some rule)
        | ["policy";"approve";"--help"] | ["policy";"baseline";"--help"] -> printfn "%s\n%s" policyWarning workflow; 0
        | ["--help"] | ["help"] -> help()
        | _ -> emit asJson false ("Unknown command.\n"+workflow)
