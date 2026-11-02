namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial
open Swensen.Unquote
open Xunit

module FiberObserverTests =
    type private Recording() =
        let gate = obj()
        let starts = ResizeArray<FiberMetadata>()
        let ends = ResizeArray<FiberMetadata * exn option>()
        let unobserved = ResizeArray<FiberMetadata option * exn>()

        member _.Observer =
            { FiberObserver.none with
                OnStart = fun metadata -> lock gate (fun () -> starts.Add metadata)
                OnEnd = fun metadata defect -> lock gate (fun () -> ends.Add(metadata, defect))
                OnUnobservedDefect = fun metadata defect -> lock gate (fun () -> unobserved.Add(metadata, defect)) }

        member _.Starts = lock gate (fun () -> List.ofSeq starts)
        member _.Ends = lock gate (fun () -> List.ofSeq ends)
        member _.Unobserved = lock gate (fun () -> List.ofSeq unobserved)

    /// A flow that waits for runtime cancellation and then settles with a defect, so a race or
    /// timeout loser has a genuine Cause.Die exit for the runtime to discard.
    let private dieOnCancel (message: string) : Flow<unit, string, unit> =
        Flow(fun _ (token: CancellationToken) ->
            Platform.ofAwaitable (task {
                try
                    do! Task.Delay(Timeout.Infinite, token)
                with _ ->
                    ()

                return Exit.Failure(Cause.Die(InvalidOperationException message))
            }))

    let private waitUntil (condition: unit -> bool) =
        TestSupport.waitUntil condition (TimeSpan.FromSeconds 5.0) |> ignore

    /// Waits for a fiber to settle without consuming its outcome, so it stays unobserved.
    /// Deterministic replacement for fixed sleeps, which race the thread pool under load.
    let rec private waitForSettled (fiber: Fiber<'error, 'value>) : Flow<unit, 'testError, unit> =
        flow {
            if fiber.Metadata.Status = FiberStatus.Running then
                do! Flow.sleep (TimeSpan.FromMilliseconds 5.0)
                return! waitForSettled fiber
        }

    [<Fact>]
    let ``Observer sees start and end for a joined fiber`` () =
        let recording = Recording()

        let result =
            flow {
                let! fiber = Flow.fork (Flow.succeed 42)
                return! Fiber.join fiber
            }
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()

        test <@ result = Exit.Success 42 @>
        test <@ recording.Starts |> List.length = 1 @>
        test <@ recording.Ends |> List.map (fun (metadata, defect) -> metadata.Status, defect) = [ FiberStatus.Succeeded, None ] @>
        test <@ recording.Unobserved = [] @>

    [<Fact>]
    let ``Discarded fork defect is reported as unobserved when the scope closes`` () =
        let recording = Recording()
        // Keep the handle alive until runSync has closed the scope. Otherwise the GC net can win
        // before scope close, and finalizer scheduling—not scope reporting—determines this test.
        let retained = ref None

        let result =
            flow {
                let! fiber = Flow.fork (Flow.die (InvalidOperationException "silent crash") : Flow<unit, string, int>)
                retained.Value <- Some fiber
                do! waitForSettled fiber
                return "done"
            }
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()

        retained.Value <- None

        test <@ result = Exit.Success "done" @>

        match recording.Unobserved with
        | [ Some metadata, defect ] ->
            test <@ defect.Message = "silent crash" @>
            test <@ metadata.Status = FiberStatus.Failed @>
        | other -> failwithf "Expected one unobserved defect with fiber metadata, got %A" other

    [<Fact>]
    let ``Joined fiber defect is not reported as unobserved`` () =
        let recording = Recording()

        let result =
            flow {
                let! fiber = Flow.fork (Flow.die (InvalidOperationException "handled crash") : Flow<unit, string, int>)
                return! Fiber.join fiber
            }
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()

        match result with
        | Exit.Failure (Cause.Die error) -> test <@ error.Message = "handled crash" @>
        | other -> failwithf "Expected defect, got %A" other

        // The joiner owns the defect; OnEnd still saw it, but it is not unobserved.
        test <@ recording.Ends |> List.map (fun (_, defect) -> defect |> Option.map _.Message) = [ Some "handled crash" ] @>
        test <@ recording.Unobserved = [] @>

    [<Fact>]
    let ``forkDetached suppresses unobserved-defect reporting`` () =
        let recording = Recording()

        let result =
            flow {
                let! fiber = Flow.forkDetached (Flow.die (InvalidOperationException "intentional") : Flow<unit, string, int>)
                do! waitForSettled fiber
                return "done"
            }
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()

        test <@ result = Exit.Success "done" @>
        // OnEnd still fires for diagnostics; the defect is just never unobserved.
        test <@ recording.Ends |> List.map (fun (_, defect) -> defect |> Option.map _.Message) = [ Some "intentional" ] @>
        test <@ recording.Unobserved = [] @>

    [<Fact>]
    let ``disconnect returns interruption promptly and reports a late defect exactly once`` () =
        let recording = Recording()
        let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        use cancellation = new CancellationTokenSource()

        let stubborn : Flow<unit, string, unit> =
            Flow(fun _ _ ->
                Platform.ofAwaitable (task {
                    started.TrySetResult() |> ignore
                    do! release.Task
                    return Exit.Failure(Cause.Die(InvalidOperationException "late disconnected defect"))
                }))

        let callerDone = TaskCompletionSource<Exit<unit, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

        let running =
            Task.Run(fun () ->
                stubborn
                |> Flow.disconnect
                |> Flow.onExit (fun exit -> Flow.delay (fun () -> callerDone.TrySetResult exit |> ignore; Flow.succeed ()))
                |> Flow.withFiberObserver recording.Observer
                |> Flow.runSyncWithToken () cancellation.Token)

        test <@ started.Task.Wait(TimeSpan.FromSeconds 5.0) @>
        cancellation.Cancel()
        // The caller stops waiting at once; the root run owns the detached work and waits for it.
        test <@ callerDone.Task.Wait(TimeSpan.FromSeconds 1.0) @>
        test <@ callerDone.Task.Result = Exit.Failure Cause.Interrupt @>
        test <@ not running.IsCompleted @>
        test <@ recording.Ends = [] @>

        release.TrySetResult() |> ignore
        test <@ running.Wait(TimeSpan.FromSeconds 5.0) @>
        test <@ running.Result = Exit.Failure Cause.Interrupt @>

        test <@ recording.Ends |> List.map (fun (_, defect) -> defect |> Option.map _.Message) = [ Some "late disconnected defect" ] @>
        test <@ recording.Unobserved |> List.map (fun (_, defect) -> defect.Message) = [ "late disconnected defect" ] @>

    [<Fact>]
    let ``Interrupted fiber ends with interrupted status and no defect`` () =
        let recording = Recording()

        let result =
            flow {
                let! fiber = Flow.fork (Flow.sleep (TimeSpan.FromSeconds 30.0) : Flow<unit, string, unit>)
                let! _exit = Fiber.interrupt fiber
                return "done"
            }
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()

        test <@ result = Exit.Success "done" @>
        test <@ recording.Ends |> List.map (fun (metadata, defect) -> metadata.Status, defect) = [ FiberStatus.Interrupted, None ] @>
        test <@ recording.Unobserved = [] @>

    [<Theory>]
    [<InlineData(false, false)>]
    [<InlineData(false, true)>]
    [<InlineData(true, false)>]
    [<InlineData(true, true)>]
    let ``Escaped loser exception is reported once without replacing winner`` (race: bool, throwingSink: bool) =
        let recording = Recording()
        let observer =
            if throwingSink then
                { recording.Observer with
                    OnUnobservedDefect = fun metadata error ->
                        recording.Observer.OnUnobservedDefect metadata error
                        failwith "reporting sink failed" }
            else recording.Observer
        let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let defect = InvalidOperationException "escaped loser"
        let loser : Flow<unit, string, int> =
            Flow(fun _ (token: CancellationToken) ->
                Platform.ofAwaitable (task {
                    started.TrySetResult() |> ignore
                    try
                        do! Task.Delay(Timeout.Infinite, token)
                    with :? OperationCanceledException -> ()
                    return raise defect
                }))
        let operation =
            if race then
                let winner = Flow.fromTask (fun _ -> task {
                    do! started.Task
                    return 42
                })
                Flow.race winner loser
            else
                loser |> Flow.timeout (TimeSpan.FromMilliseconds 20.0) "timed out"
        let result =
            operation
            |> Flow.withFiberObserver observer
            |> Flow.runSync ()
        if race then test <@ result = Exit.Success 42 @>
        else test <@ result = Exit.Failure(Cause.Fail "timed out") @>
        match recording.Unobserved with
        | [ None, reported ] -> test <@ obj.ReferenceEquals(reported, defect) @>
        | other -> failwithf "Expected exactly one escaped defect, got %A" other

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``Raw task loser cancellation is not an unobserved defect`` (race: bool) =
        let recording = Recording()
        let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let loser : Flow<unit, string, int> =
            Flow(fun _ (token: CancellationToken) ->
                Platform.ofAwaitable (task {
                    started.TrySetResult() |> ignore
                    do! Task.Delay(Timeout.Infinite, token)
                    return Exit.Success 0
                }))
        let operation =
            if race then
                let winner = Flow.fromTask (fun _ -> task {
                    do! started.Task
                    return 42
                })
                Flow.race winner loser
            else
                loser |> Flow.timeout (TimeSpan.FromMilliseconds 20.0) "timed out"
        let result =
            operation
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()
        if race then test <@ result = Exit.Success 42 @>
        else test <@ result = Exit.Failure(Cause.Fail "timed out") @>
        test <@ recording.Unobserved = [] @>

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``Task interop loser exceptions follow the Cause Die contract`` (race: bool) =
        let recording = Recording()
        let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let defect = InvalidOperationException "interop loser"
        let loser : Flow<unit, string, int> =
            Flow.fromTask (fun token -> task {
                started.TrySetResult() |> ignore
                try
                    do! Task.Delay(Timeout.Infinite, token)
                with :? OperationCanceledException -> ()
                return raise defect
            })
        let operation =
            if race then
                let winner = Flow.fromTask (fun _ -> task {
                    do! started.Task
                    return 42
                })
                Flow.race winner loser
            else
                loser |> Flow.timeout (TimeSpan.FromMilliseconds 20.0) "timed out"
        let result =
            operation
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()
        // As in race, the loser's defect is consumed: it becomes the failure, beside the timeout's own error.
        match race, result with
        | true, Exit.Failure(Cause.Die reported) -> test <@ obj.ReferenceEquals(reported, defect) @>
        | false, Exit.Failure(Cause.Both(Cause.Fail "timed out", Cause.Die reported)) ->
            test <@ obj.ReferenceEquals(reported, defect) @>
        | _, other -> failwithf "Expected the consumed loser defect, got %A" other

        test <@ recording.Unobserved = [] @>

    [<Fact>]
    let ``Timeout loser defect is returned and not observer-reported`` () =
        let recording = Recording()

        let timedOut =
            dieOnCancel "timeout loser"
            |> Flow.timeout (TimeSpan.FromMilliseconds 20.0) "timed out"
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()

        let fallback =
            dieOnCancel "timeout loser"
            |> Flow.map (fun () -> 1)
            |> Flow.timeoutToOk (TimeSpan.FromMilliseconds 20.0) 0
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()

        match timedOut, fallback with
        | Exit.Failure(Cause.Both(Cause.Fail "timed out", Cause.Die first)), Exit.Failure(Cause.Die second) ->
            test <@ first.Message = "timeout loser" && second.Message = "timeout loser" @>
        | other -> failwithf "Expected the loser defect in both results, got %A" other

        test <@ recording.Unobserved = [] @>

    [<Fact>]
    let ``Race loser defect is returned and not observer-reported`` () =
        let recording = Recording()

        let result =
            Flow.race (Flow.succeed ()) (dieOnCancel "race loser" |> Flow.map ignore)
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()

        match result with
        | Exit.Failure(Cause.Die defect) -> test <@ defect.Message = "race loser" @>
        | other -> failwithf "Expected the loser defect as the race failure, got %A" other

        test <@ recording.Unobserved = [] @>

    [<Fact>]
    let ``Observer hooks that throw do not alter outcomes`` () =
        let throwing =
            {
                OnStart = fun _ -> failwith "observer bug"
                OnEnd = fun _ _ -> failwith "observer bug"
                OnUnobservedDefect = fun _ _ -> failwith "observer bug"
            }

        let result =
            flow {
                let! fiber = Flow.fork (Flow.succeed 42)
                return! Fiber.join fiber
            }
            |> Flow.withFiberObserver throwing
            |> Flow.runSync ()

        test <@ result = Exit.Success 42 @>

    [<Fact>]
    let ``Unobserved defect is reported at most once`` () =
        let recording = Recording()
        let tracker =
            FiberDefectTracker(
                {
                    Id = FiberId 999L
                    Name = None
                    ParentId = None
                    Annotations = Map.empty
                    StartedAt = DateTimeOffset.UtcNow
                    StartedTick = TimeSpan.Zero
                    SettledAt = None
                    SettledTick = None
                    Status = FiberStatus.Failed
                },
                recording.Observer)

        tracker.Settled(Some(InvalidOperationException "once"))
        tracker.TryReport()
        tracker.TryReport()
        GC.SuppressFinalize tracker

        test <@ recording.Unobserved |> List.length = 1 @>

    [<Fact>]
    let ``Tracker respects the observed flag`` () =
        let recording = Recording()
        let metadata : FiberMetadata =
            {
                Id = FiberId 1000L
                Name = None
                ParentId = None
                Annotations = Map.empty
                StartedAt = DateTimeOffset.UtcNow
                StartedTick = TimeSpan.Zero
                SettledAt = None
                SettledTick = None
                Status = FiberStatus.Failed
            }

        let tracker = FiberDefectTracker(metadata, recording.Observer)
        tracker.Settled(Some(InvalidOperationException "boom"))
        tracker.MarkObserved()
        tracker.TryReport()
        GC.SuppressFinalize tracker

        test <@ recording.Unobserved = [] @>

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    let private allocateSettledTracker (recording: Recording) =
        let tracker =
            FiberDefectTracker(
                {
                    Id = FiberId 1001L
                    Name = None
                    ParentId = None
                    Annotations = Map.empty
                    StartedAt = DateTimeOffset.UtcNow
                    StartedTick = TimeSpan.Zero
                    SettledAt = None
                    SettledTick = None
                    Status = FiberStatus.Failed
                },
                recording.Observer)

        tracker.Settled(Some(InvalidOperationException "collected"))

    [<Fact>]
    let ``GC net reports a collected unobserved tracker through its finalizer`` () =
        let recording = Recording()
        allocateSettledTracker recording

        // Collect inside the wait loop: a single collection can miss the tracker under
        // tiered compilation, where locals live longer than their last use.
        waitUntil (fun () ->
            GC.Collect()
            GC.WaitForPendingFinalizers()
            not (List.isEmpty recording.Unobserved))

        match recording.Unobserved with
        | [ Some metadata, defect ] ->
            test <@ defect.Message = "collected" @>
            test <@ metadata.Id = FiberId 1001L @>
        | other -> failwithf "Expected one unobserved defect from the GC net, got %A" other

    [<Fact>]
    let ``settled disconnected fibers do not stay registered with the root scope`` () =
        let rootRegistrations : Flow<unit, string, int> =
            Flow(fun _ _ -> Execution.ofValue (RuntimeState.current().RootScope.RegisteredCount))

        let defective : Flow<unit, string, unit> =
            Flow.die (InvalidOperationException "disconnected defect")
            |> Flow.disconnect
            |> Flow.foldCause Flow.succeed (fun _ -> Flow.succeed ())

        let workflow =
            flow {
                let! before = rootRegistrations
                let! _ = List.replicate 100 defective |> Flow.sequence
                let! after = rootRegistrations
                return before, after
            }

        match Flow.runSync () workflow with
        | Exit.Success(before, after) -> test <@ after = before @>
        | other -> failwithf "Expected success, got %A" other
