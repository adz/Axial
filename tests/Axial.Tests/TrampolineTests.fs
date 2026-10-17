namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks.Sources
open Axial
open Swensen.Unquote
open Xunit

/// An execution awaitable that rejects a second consumption, unlike a Task-backed ValueTask.
type private SingleUseExecutionSource<'value, 'error>() as this =
    let mutable core = ManualResetValueTaskSourceCore<Exit<'value, 'error>>()
    let mutable reads = 0
    let registered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
    do core.RunContinuationsAsynchronously <- true

    member _.Execution = ValueTask<Exit<'value, 'error>>(this :> IValueTaskSource<_>, core.Version)
    member _.Registered = registered.Task
    member _.Reads = Volatile.Read(&reads)
    member _.Complete exit = core.SetResult exit

    interface IValueTaskSource<Exit<'value, 'error>> with
        member _.GetStatus token = core.GetStatus token

        member _.GetResult token =
            if Interlocked.Increment(&reads) <> 1 then invalidOp "Execution was consumed twice."
            core.GetResult token

        member _.OnCompleted(continuation, state, token, flags) =
            core.OnCompleted(continuation, state, token, flags)
            registered.TrySetResult() |> ignore

module TrampolineTests =
    let private suspend () : Flow<'env, 'error, unit> =
        Flow.fromTask (fun _ -> task { do! Task.Yield() })

    [<Fact>]
    let ``Recursive flows stay cold and can be run more than once`` () =
        let mutable visits = 0
        let rec count remaining : Flow<unit, string, int> =
            flow {
                visits <- visits + 1
                if remaining = 0 then return visits
                else return! count (remaining - 1)
            }

        let workflow = count 1000
        test <@ visits = 0 @>
        test <@ workflow.RunSynchronously(()) = Exit.Success 1001 @>
        test <@ workflow.RunSynchronously(()) = Exit.Success 2002 @>

    [<Fact>]
    let ``Tail recursion sequences mixed synchronous and asynchronous sources with different value types`` () =
        let rec count index total : Flow<unit, string, int> =
            flow {
                if index = 20_000 then return total
                else
                    let! text = Flow.ok (string index)
                    do! suspend ()
                    let! length = Flow.ok text.Length
                    return! count (index + 1) (total + length)
            }

        let expected = [0..19_999] |> List.sumBy (string >> String.length)
        test <@ count 0 0 |> Flow.runSync () = Exit.Success expected @>

    [<Fact>]
    let ``Non-tail recursive binds keep their pending work in order`` () =
        let completed = ResizeArray<int>()
        let rec sum remaining : Flow<unit, string, int> =
            flow {
                if remaining = 0 then return 0
                else
                    if remaining % 100 = 0 then do! suspend ()
                    let! subtotal = sum (remaining - 1)
                    completed.Add remaining
                    return subtotal + remaining
            }

        test <@ sum 2000 |> Flow.runSync () = Exit.Success 2_001_000 @>
        test <@ List.ofSeq completed = [1..2000] @>

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``A recursive source failure skips its binder and unwinds lexical cleanup`` suspends =
        let released = ResizeArray<int>()
        let rec consume index : Flow<unit, string, unit> =
            flow {
                try
                    if suspends then do! suspend ()
                    if index = 100 then return! Flow.fail "stop"
                    return! consume (index + 1)
                finally
                    released.Add index
            }

        test <@ consume 0 |> Flow.runSync () = Exit.Failure(Cause.Fail "stop") @>
        test <@ List.ofSeq released = [100.. -1 ..0] @>

    [<Fact>]
    let ``Recursive continuations restore the outer runtime after an asynchronous scoped source`` () =
        let rec consume remaining : Flow<unit, string, unit> =
            flow {
                let! inner =
                    flow {
                        do! suspend ()
                        return! Flow.annotations
                    }
                    |> Flow.annotate "region" "inner"

                let! outer = Flow.annotations
                test <@ inner["region"] = "inner" @>
                test <@ outer["region"] = "outer" @>
                if remaining > 0 then return! consume (remaining - 1)
            }

        let workflow = consume 1000 |> Flow.annotate "region" "outer"
        test <@ workflow |> Flow.runSync () = Exit.Success () @>

    [<Fact>]
    let ``Cancellation interrupts a recursive flow and runs its cleanup`` () =
        let cancellation = new CancellationTokenSource()
        let mutable cleaned = false
        let rec consume index : Flow<unit, string, unit> =
            flow {
                do! suspend ()
                if index = 1000 then
                    cancellation.Cancel()
                    return! Flow.never
                else return! consume (index + 1)
            }

        let workflow =
            consume 0
            |> Flow.ensuring (Flow.delay (fun () -> cleaned <- true; Flow.ok ()))

        task {
            use ownedCancellation = cancellation
            let! exit = workflow.StartAsTask((), ownedCancellation.Token).WaitAsync(TimeSpan.FromSeconds 10.0)
            test <@ exit = Exit.Failure Cause.Interrupt @>
            test <@ cleaned @>
        }

    [<Fact>]
    let ``Foreign cancellation remains a defect in a recursive flow`` () =
        let foreign = OperationCanceledException "foreign cancellation"
        let rec consume index : Flow<unit, string, unit> =
            flow {
                do! suspend ()
                if index = 1000 then
                    return! Flow.fromTask (fun _ -> Task.FromException<unit> foreign)
                else return! consume (index + 1)
            }

        match consume 0 |> Flow.runSync () with
        | Exit.Failure(Cause.Die error) -> test <@ obj.ReferenceEquals(error, foreign) @>
        | other -> failwithf "Expected a foreign cancellation defect, got %A" other

    [<Fact>]
    let ``A pending bind consumes an IValueTaskSource execution exactly once`` () =
        task {
            let source = SingleUseExecutionSource<int, string>()
            // Exercise the executor boundary directly: public ValueTask interop wraps this source in a Task first.
            let workflow : Flow<unit, string, int> =
                Flow(fun _ _ -> source.Execution)
                |> Flow.bind (fun value -> Flow.ok (value + 1))

            let running = workflow.StartAsTask(())
            do! source.Registered.WaitAsync(TimeSpan.FromSeconds 10.0)
            source.Complete (Exit.Success 41)
            let! exit = running.WaitAsync(TimeSpan.FromSeconds 10.0)
            test <@ exit = Exit.Success 42 @>
            test <@ source.Reads = 1 @>
        }
