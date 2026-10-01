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
                match (scenario.Run(Round(seed, 100))) |> Flow.runSync (TestSupport.clockEnv ()) with
                | Exit.Success checks -> checks |> List.filter (fun check -> not check.Held) |> List.map _.Invariant
                | Exit.Failure cause -> [ $"the round completed (it ended with {cause})" ]

            test <@ (seed, violated) = (seed, []) @>
