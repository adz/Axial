namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Swensen.Unquote
open Xunit

module AppTests =
    [<Fact>]
    let ``App start exposes completion and completed status`` () =
        let running = App.start "environment" (Flow.read (fun value -> value.Length))

        let exit = running.Completion |> Async.RunSynchronously

        test <@ exit = Exit.Success 11 @>
        test <@ running.Status = AppStatus.Completed @>

    [<Fact>]
    let ``App stop interrupts once and waits for root finalizers`` () =
        let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let finalized = ResizeArray<bool>()

        let application : Flow<unit, string, unit> =
            flow {
                do! Flow.addFinalizerAsync(fun token -> async {
                    finalized.Add token.IsCancellationRequested
                })
                started.TrySetResult() |> ignore
                do! Flow.Runtime.sleep(TimeSpan.FromSeconds 30.0)
            }

        let running = App.start () application
        test <@ started.Task.Wait(TimeSpan.FromSeconds 2.0) @>

        let first = running.Stop() |> Async.StartAsTask
        let second = running.Stop() |> Async.StartAsTask
        Task.WaitAll [| first :> Task; second :> Task |]

        test <@ first.Result = Exit.Failure Cause.Interrupt @>
        test <@ second.Result = first.Result @>
        test <@ List.ofSeq finalized = [ true ] @>
        test <@ running.Status = AppStatus.Completed @>

    [<Fact>]
    let ``App external cancellation requests stop`` () =
        use cancellationSource = new CancellationTokenSource()

        let running =
            (Flow.Runtime.sleep(TimeSpan.FromSeconds 30.0) : Flow<unit, string, unit>)
            |> App.startWithCancellation cancellationSource.Token ()

        cancellationSource.Cancel()
        let exit = running.Completion |> Async.RunSynchronously

        test <@ exit = Exit.Failure Cause.Interrupt @>
        test <@ running.Status = AppStatus.Completed @>

    [<Fact>]
    let ``App run preserves typed failure`` () =
        let exit =
            (Flow.fail "configuration failed" : Flow<int, string, unit>)
            |> App.run 42
            |> Async.RunSynchronously

        test <@ exit = Exit.Failure(Cause.Fail "configuration failed") @>
