namespace Axial.Elmish.Tests

open System
open Axial
open Axial.Elmish
open Xunit

module TestRuntimeTests =
    [<Fact>]
    let ``test runtime queues delivery and collects defects`` () =
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        use test = ElmishRuntime.test (ClockEnvironment(clock))
        TestRuntime.run (Cmd.OfFlow.perform "answer" test.Runtime (Flow.succeed 42) id) test
        Assert.True(TestRuntime.settle (TimeSpan.FromSeconds 5.0) test)
        Assert.Equal<obj list>([ box 42 ], TestRuntime.dispatched test)
        TestRuntime.run (Cmd.OfFlow.perform "broken" test.Runtime (Flow.die (Exception("boom"))) id) test
        Assert.True(TestRuntime.settle (TimeSpan.FromSeconds 5.0) test)
        Assert.Single(TestRuntime.defects test) |> ignore
