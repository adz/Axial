namespace Axial.Tests

open System
open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open Axial
open Swensen.Unquote
open Xunit

type AppShutdownTests() =
    [<Fact>]
    member _.``stop from a single threaded context runs release without pumping`` () =
        let previous = SynchronizationContext.Current
        SynchronizationContext.SetSynchronizationContext(SynchronizationContext())
        try
            let started = new ManualResetEventSlim()
            let released = new ManualResetEventSlim()
            let application : Flow<unit, string, unit> =
                flow {
                    do! Flow.scopeFinalizer (Flow.delay (fun () -> released.Set(); Flow.succeed ()))
                    started.Set()
                    return! Flow.never
                }
            let running = App.start () application
            test <@ started.Wait(TimeSpan.FromSeconds 2.) @>
            let elapsed = Stopwatch.StartNew()
            test <@ running.StopAndWait(TimeSpan.FromSeconds 5.) @>
            test <@ released.IsSet @>
            test <@ elapsed.Elapsed < TimeSpan.FromSeconds 3. @>
        finally
            SynchronizationContext.SetSynchronizationContext(previous)

    [<Fact>]
    member _.``root synchronous prefix runs on another thread`` () =
        let caller = Thread.CurrentThread.ManagedThreadId
        let started = TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
        let application : Flow<unit, string, unit> =
            flow {
                started.TrySetResult(Thread.CurrentThread.ManagedThreadId) |> ignore
                return! Flow.never
            }
        let running = App.start () application
        test <@ started.Task.Wait(TimeSpan.FromSeconds 2.) @>
        test <@ started.Task.Result <> caller @>
        test <@ running.StopAndWait(TimeSpan.FromSeconds 5.) @>

    [<Fact>]
    member _.``stop timeout includes unfinished release`` () =
        let started = new ManualResetEventSlim()
        let releaseGate = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let application : Flow<unit, string, unit> =
            flow {
                do! Flow.scopeFinalizer (Flow.fromTask (fun _ -> releaseGate.Task))
                started.Set()
                return! Flow.never
            }
        let running = App.start () application
        test <@ started.Wait(TimeSpan.FromSeconds 2.) @>
        let elapsed = Stopwatch.StartNew()
        let settled = running.StopAndWait(TimeSpan.FromMilliseconds 200.)
        test <@ not settled @>
        test <@ elapsed.Elapsed >= TimeSpan.FromMilliseconds 150. @>
        test <@ elapsed.Elapsed < TimeSpan.FromSeconds 3. @>
        releaseGate.SetResult()
        test <@ running.StopAndWait(TimeSpan.FromSeconds 5.) @>

module AppTests =
    [<Fact>]
    let ``startWith keeps cache lookups alive after publishing`` () =
        let environment = TestSupport.clockEnv ()
        let build = Cache.make (fun key -> Flow.succeed (key + 1))
        let running, cache =
            match App.startWith environment build |> Async.RunSynchronously with
            | Ok value -> value
            | Error _ -> failwith "build failed"
        let result = Cache.get 41 cache |> Flow.run environment
        test <@ result = Exit.Success 42 @>
        test <@ running.StopAndWait(TimeSpan.FromSeconds 5.) @>

    [<Fact>]
    let ``startWith stop waits for build finalizer`` () =
        let released = new ManualResetEventSlim()
        let build : Flow<unit, string, int> = flow {
            do! Flow.scopeFinalizer (Flow.delay (fun () -> released.Set(); Flow.succeed ()))
            return 7
        }
        let running, value = App.startWith () build |> Async.RunSynchronously |> function Ok value -> value | Error _ -> failwith "build failed"
        test <@ value = 7 @>
        test <@ not released.IsSet @>
        test <@ running.StopAndWait(TimeSpan.FromSeconds 5.) @>
        test <@ released.IsSet @>

    [<Fact>]
    let ``startWith failure leaves no running scope`` () =
        let released = new ManualResetEventSlim()
        let build : Flow<unit, string, int> = flow {
            do! Flow.scopeFinalizer (Flow.delay (fun () -> released.Set(); Flow.succeed ()))
            return! Flow.fail "failed"
        }
        let result = App.startWith () build |> Async.RunSynchronously
        test <@ Result.isError result @>
        test <@ released.IsSet @>

    [<Fact>]
    let ``startWith handles own independent scopes`` () =
        let build : Flow<unit, string, unit> = Flow.succeed ()
        let first, _ = App.startWith () build |> Async.RunSynchronously |> function Ok value -> value | Error _ -> failwith "build failed"
        let second, _ = App.startWith () build |> Async.RunSynchronously |> function Ok value -> value | Error _ -> failwith "build failed"
        test <@ first.StopAndWait(TimeSpan.FromSeconds 5.) @>
        test <@ second.Status = AppStatus.Running @>
        test <@ second.StopAndWait(TimeSpan.FromSeconds 5.) @>

    [<Fact>]
    let ``App start exposes completion and completed status`` () =
        let running = App.start "environment" (Flow.envWith (fun value -> value.Length))

        let exit = running.Completion |> Async.RunSynchronously

        test <@ exit = Exit.Success 11 @>
        test <@ running.Status = AppStatus.Completed @>

    [<Fact>]
    let ``App stop interrupts once and waits for root finalizers`` () =
        let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let finalized = ResizeArray<bool>()

        let application : Flow<ClockEnvironment, string, unit> =
            flow {
                do!
                    Flow.scopeFinalizer (
                        Flow.cancellationToken
                        |> Flow.map (fun token -> finalized.Add token.IsCancellationRequested)
                    )
                started.TrySetResult() |> ignore
                do! Flow.sleep(TimeSpan.FromSeconds 30.0)
            }

        let running = App.start (TestSupport.clockEnv ()) application
        test <@ started.Task.Wait(TimeSpan.FromSeconds 2.0) @>

        let first = running.Stop() |> Async.StartAsTask
        let second = running.Stop() |> Async.StartAsTask
        Task.WaitAll [| first :> Task; second :> Task |]

        test <@ first.Result = Exit.Failure Cause.Interrupt @>
        test <@ second.Result = first.Result @>
        // The release runs without interruption, even though stopping interrupted the application.
        test <@ List.ofSeq finalized = [ false ] @>
        test <@ running.Status = AppStatus.Completed @>

    [<Fact>]
    let ``App external cancellation requests stop`` () =
        use cancellationSource = new CancellationTokenSource()

        let running =
            (Flow.sleep(TimeSpan.FromSeconds 30.0) : Flow<ClockEnvironment, string, unit>)
            |> App.startWithCancellation cancellationSource.Token (TestSupport.clockEnv ())

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
