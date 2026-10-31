namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial
open Xunit

type private ThrowingDisposable(error: exn) =
    interface IDisposable with
        member _.Dispose() = raise error

module CoreCancellationClassificationTests =
    let private cleanupFailure () = OperationCanceledException "cleanup"

    let private disposableFlow (error: exn) : Flow<unit, string, int> =
        flow {
            do! Flow.scopeDisposable (new ThrowingDisposable(error))
            return 1
        }

    [<Fact>]
    let ``cleanup cancellation is a defect at root and fork boundaries`` () =
        let cleanup = cleanupFailure ()

        let root = Flow.scoped (disposableFlow cleanup) |> Flow.runSync ()
        Assert.Equal(Exit.Failure(Cause.Die cleanup), root)

        let forked =
            flow {
                let! fiber = Flow.fork (Flow.scoped (disposableFlow cleanup))
                return! Fiber.join fiber
            }
            |> Flow.runSync ()

        Assert.Equal(Exit.Failure(Cause.Die cleanup), forked)

    [<Fact>]
    let ``race does not swallow a loser cleanup cancellation`` () =
        let cleanup = cleanupFailure ()
        let acquired = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        let loser : Flow<unit, string, int> =
            flow {
                do! Flow.scopeDisposable (new ThrowingDisposable(cleanup))
                acquired.SetResult()
                return! Flow.never
            }

        let winner : Flow<unit, string, int> =
            Flow.fromTask (fun _ ->
                task {
                    do! acquired.Task
                    return 1
                })

        let result = Flow.race winner loser |> Flow.runSync ()
        Assert.Equal(Exit.Failure(Cause.Die cleanup), result)

    [<Fact>]
    let ``timeout reports a timed-out cleanup cancellation as a defect`` () =
        let cleanup = cleanupFailure ()

        let timed : Flow<unit, string, int> =
            flow {
                do! Flow.scopeDisposable (new ThrowingDisposable(cleanup))
                return! Flow.never
            }

        let result =
            Flow.timeout (TimeSpan.FromMilliseconds 100.) "timeout" timed
            |> Flow.runSync ()

        Assert.Equal(Exit.Failure(Cause.Die cleanup), result)

    [<Fact>]
    let ``retry reports a superseded cleanup cancellation as a defect`` () =
        let cleanup = cleanupFailure ()
        let first = ref true

        let workflow : Flow<unit, string, int> =
            Flow.delay (fun () ->
                if first.Value then
                    first.Value <- false
                    flow {
                        do! Flow.scopeDisposable (new ThrowingDisposable(cleanup))
                        return! Flow.fail "retry"
                    }
                else
                    Flow.ok 1)
            |> Flow.retry (Schedule.recurs 1)

        Assert.Equal(Exit.Failure(Cause.Then(Cause.Fail "retry", Cause.Die cleanup)), Flow.runSync () workflow)

    [<Fact>]
    let ``repeat reports a superseded cleanup cancellation as a defect`` () =
        let cleanup = cleanupFailure ()
        let first = ref true

        let workflow : Flow<unit, string, int> =
            Flow.delay (fun () ->
                if first.Value then
                    first.Value <- false
                    flow {
                        do! Flow.scopeDisposable (new ThrowingDisposable(cleanup))
                        return 1
                    }
                else
                    Flow.ok 2)
            |> Flow.repeat (Schedule.recurs 1)

        Assert.Equal(Exit.Failure(Cause.Die cleanup), Flow.runSync () workflow)

    [<Fact>]
    let ``supervise reports a superseded cleanup cancellation as a defect`` () =
        let cleanup = cleanupFailure ()
        let bodyDefect = InvalidOperationException "body"
        let first = ref true

        let workflow : Flow<unit, string, int> =
            Flow.delay (fun () ->
                if first.Value then
                    first.Value <- false
                    flow {
                        do! Flow.scopeDisposable (new ThrowingDisposable(cleanup))
                        return! Flow.die bodyDefect
                    }
                else
                    Flow.ok 1)
            |> Flow.supervise (Schedule.recurs 1)

        let result = Flow.runSync () workflow
        Assert.Equal(Exit.Failure(Cause.Then(Cause.Die bodyDefect, Cause.Die cleanup)), result)

    [<Fact>]
    let ``zipPar keeps a sibling failure when the first branch was canceled`` () =
        use canceled = new CancellationTokenSource()
        canceled.Cancel()

        let canceledBranch : Flow<unit, string, int> =
            Flow(fun (_: unit) _ ->
                ValueTask<Exit<int, string>>(Task.FromCanceled<Exit<int, string>> canceled.Token))

        let failingBranch : Flow<unit, string, int> =
            Flow(fun (_: unit) _ ->
                ValueTask<Exit<int, string>>(
                    task {
                        do! Task.Delay 20
                        return Exit.Failure(Cause.Fail "right")
                    }
                ))

        let result = Flow.zipPar canceledBranch failingBranch |> Flow.runSync ()
        Assert.Equal(Exit.Failure(Cause.Fail "right"), result)
