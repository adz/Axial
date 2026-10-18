namespace Axial.Elmish.Tests

open System
open System.Threading.Tasks
open Axial
open Axial.ElmishFlows
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

    [<Fact>]
    let ``stop drains jobs not yet started`` () =
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        use test = ElmishRuntime.test (ClockEnvironment(clock))
        for i in 1 .. 1000 do
            TestRuntime.run (Cmd.OfFlow.perform "queued" test.Runtime (Flow.succeed i) id) test
        Assert.True(test.Runtime.StopAndWait(TimeSpan.FromSeconds 5.0))
        Assert.True(Task.Run(fun () -> TestRuntime.settle (TimeSpan.FromSeconds 5.0) test).Result)
