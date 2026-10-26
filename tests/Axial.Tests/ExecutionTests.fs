namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Axial

module ExecutionTests =

    [<Fact>]
    let ``Flow ToAsync can be converted to Result within async block on .NET`` () =
        async {
            let flow = Flow.ok 42
            let! result = flow.ToAsync(())
            Assert.Equal(Exit.Success 42, result)
            Assert.Equal(Ok 42, Exit.toResult result)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``Flow ToAsync preserves typed failure in Exit`` () =
        async {
            let flow = Flow.fail "oops"
            let! result = flow.ToAsync(())
            Assert.Equal(Exit.Failure(Cause.Fail "oops"), result)
            Assert.Equal(Error "oops", Exit.toResult result)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``Flow ToTask can be converted to Result within task block`` () =
        task {
            let flow = Flow.ok 42
            let! result = flow.StartAsTask(())
            Assert.Equal(Exit.Success 42, result)
            Assert.Equal(Ok 42, Exit.toResult result)
        } :> Task

    [<Fact>]
    let ``Flow.toAsync observes ambient cancellation`` () =
        use cts = new CancellationTokenSource()
        let flow = Flow.sleep (TimeSpan.FromSeconds 10.0)
        let running = Async.StartAsTask(flow.ToAsync(TestSupport.clockEnv ()), cancellationToken = cts.Token)

        cts.Cancel()

        // A sleep that ignored the token would still be running when this deadline expires.
        let settled = (running :> Task).ContinueWith(fun (_: Task) -> ()).Wait(TimeSpan.FromSeconds 5.0)
        Assert.True(settled)
        Assert.True(running.IsCanceled || (running.IsCompletedSuccessfully && running.Result = Exit.Failure Cause.Interrupt))
