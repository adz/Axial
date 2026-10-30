/// The process entry point: runs scenarios for a number of rounds and reports each invariant.
module Axial.TortureTest.Program

open Axial
open Axial.TortureTest.Scenario

// <snippet:torture-runner>
/// How often a slow round reports progress, and how long a round may run before it counts as stalled.
let progressEvery = System.TimeSpan.FromSeconds 10.0
let stallAfter = System.TimeSpan.FromSeconds 60.0

/// Runs one round in a fiber tracked by a fiber registry, turning a round that fails outright into a violated check.
/// A round slower than <c>progressEvery</c> reports its live fiber count. A round past <c>stallAfter</c> reports the
/// live fiber tree, interrupts every live fiber, and fails with a stall check, so a hang names the fibers still waiting
/// instead of blocking the run. Returns <c>None</c> if the round's fibers still do not settle after the interrupt.
let watchRound (say: string -> unit) (scenario: Scenario) (seed: int) : Flow<unit, Never, Check list option> =
    let registry = FiberRegistry()
    let clock = Axial.PlatformService.Clock.live
    let failed (cause: Cause<Never>) =
        let reason = Cause.prettyPrint (fun (_: Never) -> "") cause
        [ check $"the round completed (it ended with {reason})" false ]
    let rec settle round (deadline: System.TimeSpan) =
        flow {
            match! Fiber.poll round with
            | Some _ ->
                let! exit = Fiber.awaitExit round
                return Some exit
            | None when clock.Elapsed() >= deadline -> return None
            | None ->
                do! Flow.sleep (System.TimeSpan.FromMilliseconds 50.0)
                return! settle round deadline
        }
    flow {
        // The round's own fiber is forked under the registry, so the dump shows it as the root of the tree.
        let! round =
            Flow.forkNamed $"{scenario.Name} seed {seed}" (scenario.Run(Round(seed, 100)))
            |> Flow.withFiberRegistry registry
        let started = clock.Elapsed()
        let rec watch () =
            flow {
                match! settle round (clock.Elapsed() + progressEvery) with
                | Some(Exit.Success checks) -> return Some checks
                | Some(Exit.Failure cause) -> return Some(failed cause)
                | None when clock.Elapsed() - started < stallAfter ->
                    say $"{scenario.Name} seed {seed}: running {(clock.Elapsed() - started).TotalSeconds:F0}s, {registry.LiveFiberCount} live fiber(s), {registry.StartedCount} started"
                    return! watch ()
                | None ->
                    let dump = registry.DumpAt clock
                    say $"{scenario.Name} seed {seed}: STALLED after {stallAfter.TotalSeconds:F0}s"
                    say dump
                    for fiber in registry.Snapshot() do
                        registry.Interrupt fiber.Id |> ignore
                    match! settle round (clock.Elapsed() + progressEvery) with
                    | Some _ -> return Some [ check $"the round finished within {stallAfter.TotalSeconds:F0}s (see the fiber dump)" false ]
                    | None -> return None
            }
        return! watch ()
    }

/// Runs one round and exits with code 3 when a stalled round cannot be interrupted, because its fibers would block
/// the run from finishing.
let runRound (scenario: Scenario) (seed: int) : Check list =
    match (watchRound (printfn "%s") scenario seed).RunSynchronously(clockEnvironment) with
    | Exit.Success(Some checks) -> checks
    | Exit.Success None ->
        printfn "%s seed %d: the stalled round ignored interruption; exiting" scenario.Name seed
        exit 3
    | Exit.Failure _ -> [ check "the round's watchdog completed" false ]

/// Runs every round of a scenario, prints each invariant with the rounds it held on and the seeds it failed with,
/// and returns whether every invariant held on every round.
let report (scenario: Scenario) (rounds: int) : bool =
    let results = [ for seed in 1..rounds -> seed, runRound scenario seed ]
    let invariants = results |> List.collect (snd >> List.map _.Invariant) |> List.distinct
    printfn "%s: %s" scenario.Name scenario.Title

    for invariant in invariants do
        let failedSeeds =
            [ for seed, checks in results do
                  if checks |> List.exists (fun check -> check.Invariant = invariant && not check.Held) then seed ]

        match failedSeeds with
        | [] -> printfn "  ✔ %s (%d/%d)" invariant rounds rounds
        | seeds ->
            let listed = seeds |> List.map string |> String.concat ", "
            printfn "  ✗ %s (%d/%d; failed with seeds %s)" invariant (rounds - seeds.Length) rounds listed

    results |> List.forall (snd >> List.forall _.Held)

/// Usage: <c>dotnet run -- [scenario | all] [rounds]</c>. Runs every scenario for 10 rounds by default, and exits
/// with a non-zero code if any invariant was violated.
[<EntryPoint>]
let main arguments =
    let selected, rounds =
        match arguments with
        | [||] -> Some Scenarios.all, 10
        | [| name |] when name = "all" -> Some Scenarios.all, 10
        | [| name |] when Seq.forall System.Char.IsDigit name -> Some Scenarios.all, int name
        | [| name |] -> Scenarios.tryFind name |> Option.map List.singleton, 10
        | [| name; count |] when name = "all" -> Some Scenarios.all, int count
        | [| name; count |] -> Scenarios.tryFind name |> Option.map List.singleton, int count
        | _ -> None, 0

    match selected with
    | None ->
        let names = Scenarios.all |> List.map _.Name |> String.concat ", "
        printfn "Usage: dotnet run -- [scenario | all] [rounds]. Scenarios: %s" names
        2
    | Some scenarios ->
        let results = [ for scenario in scenarios -> report scenario rounds ]
        if List.forall id results then 0 else 1
// </snippet:torture-runner>
