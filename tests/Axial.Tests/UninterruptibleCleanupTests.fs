namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial
open Xunit

module UninterruptibleCleanupTests =
    let private signal () = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
    let private deadline = TimeSpan.FromSeconds 10.0

    [<Fact>]
    let ``internal cleanup runner supplies an inert token and preserves the operation`` () =
        use caller = new CancellationTokenSource()
        caller.Cancel()
        let mutable calls = 0
        let result =
            Platform.Cancellation.uninterruptible (fun token ->
                calls <- calls + 1
                Assert.False(token.CanBeCanceled)
                token.ThrowIfCancellationRequested()
                42)
        Assert.Equal(42, result)
        Assert.Equal(1, calls)
        let defect = InvalidOperationException("cleanup defect")
        let raised = Assert.Throws<InvalidOperationException>(Action(fun () ->
            Platform.Cancellation.uninterruptible (fun _ -> raise defect)))
        Assert.Same(defect, raised)

    // The first gate proves cancellation happens during cleanup, not before acquisition or after settlement.
    // The second gate keeps cleanup suspended until the test has checked that the owner is still waiting.
    let private suspendedCleanup (started: TaskCompletionSource<unit>) (resume: TaskCompletionSource<unit>)
        (finished: TaskCompletionSource<unit>) : Flow<ClockEnvironment, Never, unit> =
        Flow(fun _ token ->
            task {
                Assert.False(token.CanBeCanceled)
                started.SetResult()
                do! resume.Task.WaitAsync(token)
                token.ThrowIfCancellationRequested()
                finished.SetResult()
                return Exit.Success ()
            }
            |> Platform.ofAwaitable)

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``cleanup keeps the original failure when caller cancels during ensuring or acquireRelease`` (resource: bool) =
        task {
            use caller = new CancellationTokenSource()
            let started, resume, finished = signal (), signal (), signal ()
            let cleanup = suspendedCleanup started resume finished
            let source : Flow<ClockEnvironment, string, unit> =
                if resource then
                    flow {
                        use! _value = Resource.create (Flow.ok 1) (fun _ -> cleanup)
                        return! Flow.fail "original"
                    }
                else
                    Flow.fail "original" |> Flow.ensuring cleanup
            let running = source.StartAsTask(TestSupport.clockEnv (), caller.Token)
            try
                do! started.Task.WaitAsync(deadline)
                caller.Cancel()
                Assert.False(running.IsCompleted)
                Assert.False(finished.Task.IsCompleted)
            finally
                resume.TrySetResult() |> ignore
            let! exit = running.WaitAsync(deadline)
            Assert.Equal<Exit<unit, string>>(Exit.Failure(Cause.Fail "original"), exit)
            Assert.True(finished.Task.IsCompletedSuccessfully)
        }

    [<Fact>]
    let ``race waits for loser cleanup when caller cancels mid release`` () =
        task {
            use caller = new CancellationTokenSource()
            let acquired, started, resume, finished = signal (), signal (), signal (), signal ()
            let loser : Flow<ClockEnvironment, string, int> =
                flow {
                    use! _value = Resource.create (Flow.ok 1) (fun _ -> suspendedCleanup started resume finished)
                    acquired.SetResult()
                    return! Flow.never
                }
            let winner : Flow<ClockEnvironment, string, int> =
                flow {
                    do! Flow.fromTask (fun token -> acquired.Task.WaitAsync(token))
                    return 42
                }
            let running = Flow.race loser winner |> fun source -> source.StartAsTask(TestSupport.clockEnv (), caller.Token)
            try
                do! started.Task.WaitAsync(deadline)
                caller.Cancel()
                Assert.False(running.IsCompleted)
                Assert.False(finished.Task.IsCompleted)
            finally
                resume.TrySetResult() |> ignore
            let! exit = running.WaitAsync(deadline)
            Assert.Equal<Exit<int, string>>(Exit.Success 42, exit)
            Assert.True(finished.Task.IsCompletedSuccessfully)
        }

    [<Fact>]
    let ``explicit scope disposal waits for finalizers after caller cancellation`` () =
        task {
            use caller = new CancellationTokenSource()
            let started, resume, finished = signal (), signal (), signal ()
            let scope = new Scope()
            scope.AddFinalizer(fun token ->
                task {
                    Assert.False(token.CanBeCanceled)
                    started.SetResult()
                    do! resume.Task.WaitAsync(token)
                    finished.SetResult()
                } :> Task)
            let closing = (scope :> IAsyncDisposable).DisposeAsync().AsTask()
            try
                do! started.Task.WaitAsync(deadline)
                caller.Cancel()
                Assert.False(closing.IsCompleted)
            finally
                resume.TrySetResult() |> ignore
            do! closing.WaitAsync(deadline)
            Assert.True(finished.Task.IsCompletedSuccessfully)
            Assert.Equal(0, scope.RegisteredCount)
        }
