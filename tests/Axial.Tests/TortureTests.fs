namespace Axial.Tests

open Axial
open Axial.TortureTest
open Swensen.Unquote
open Xunit

/// Runs the pipeline from the "Torture test" docs page (examples/Axial.TortureTest) on every build. Interleavings
/// differ on every run, so each run must keep every invariant.
module TortureTests =
    [<Fact>]
    let ``The torture pipeline keeps every invariant on every run`` () =
        for _ in 1..20 do
            let violated =
                match Pipeline.run () |> Flow.runSync () with
                | Exit.Success checks -> checks |> List.filter (fun check -> not check.Held) |> List.map _.Invariant
                | Exit.Failure cause -> [ $"the run ended with {cause}" ]

            test <@ violated = [] @>
