/// The process entry point: runs the pipeline for a number of rounds and reports each invariant.
module Axial.TortureTest.Program

open Axial
open Axial.TortureTest.Pipeline

// <snippet:torture-runner>
/// Runs the pipeline the given number of times, prints each invariant with how many runs it held on, and fails the
/// process if any invariant was ever violated.
[<EntryPoint>]
let main arguments =
    let rounds =
        match arguments with
        | [| count |] -> int count
        | _ -> 10

    let runs =
        [ for round in 1..rounds ->
              match (run ()).RunSynchronously(()) with
              | Exit.Success checks -> checks
              | Exit.Failure cause ->
                  let reason = Cause.prettyPrint (fun (_: Never) -> "") cause
                  failwith $"Round {round} ended with {reason}" ]

    printfn "Ran the pipeline %d times: %d readers x %d samples each time." rounds readers samplesPerReader

    for invariant in runs.Head |> List.map _.Invariant do
        let held = runs |> List.filter (List.exists (fun check -> check.Invariant = invariant && check.Held)) |> List.length
        printfn "%s %s (%d/%d)" (if held = rounds then "✔" else "✗") invariant held rounds

    if runs |> List.forall (List.forall _.Held) then 0 else 1
// </snippet:torture-runner>
