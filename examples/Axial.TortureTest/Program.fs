/// The process entry point: runs scenarios for a number of rounds and reports each invariant.
module Axial.TortureTest.Program

open Axial
open Axial.TortureTest.Scenario

// <snippet:torture-runner>
/// Runs one round, turning a round that fails outright into a violated check.
let runRound (scenario: Scenario) (seed: int) : Check list =
    match (scenario.Run(Round(seed, 100))).RunSynchronously(()) with
    | Exit.Success checks -> checks
    | Exit.Failure cause ->
        let reason = Cause.prettyPrint (fun (_: Never) -> "") cause
        [ check $"the round completed (it ended with {reason})" false ]

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
