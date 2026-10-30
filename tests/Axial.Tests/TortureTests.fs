namespace Axial.Tests

open Axial
open Axial.TortureTest
open Axial.TortureTest.Scenario
open Swensen.Unquote
open Xunit

/// Runs every scenario from examples/Axial.TortureTest (the "Torture tests" docs pages) on every build. Interleavings
/// differ on every run, so each round must keep every invariant.
module TortureTests =
    let scenarioNames () : obj array seq = Scenarios.all |> Seq.map (fun scenario -> [| box scenario.Name |])

    [<Theory>]
    [<MemberData(nameof scenarioNames)>]
    let ``A torture scenario keeps every invariant on every round`` (name: string) =
        let scenario = Scenarios.tryFind name |> Option.get

        for seed in 1..10 do
            let violated =
                // The watchdog reports a stalled round with its live fiber tree instead of hanging the test run.
                let said = System.Collections.Concurrent.ConcurrentQueue<string>()
                match Program.watchRound said.Enqueue scenario seed |> Flow.runSync () with
                | Exit.Success(Some checks) ->
                    let violated = checks |> List.filter (fun check -> not check.Held) |> List.map _.Invariant
                    if List.isEmpty violated then [] else violated @ List.ofSeq said
                | Exit.Success None -> "the stalled round ignored interruption" :: List.ofSeq said
                | Exit.Failure cause -> [ $"the round's watchdog completed (it ended with {cause})" ]

            test <@ (seed, violated) = (seed, []) @>
