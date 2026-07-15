namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial.Flow
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
        let mutable remaining = 500

        while not (condition ()) && remaining > 0 do
            remaining <- remaining - 1
            Thread.Sleep 10

    /// Waits for a fiber to settle without consuming its outcome, so it stays unobserved.
    /// Deterministic replacement for fixed sleeps, which race the thread pool under load.
    let rec private waitForSettled (fiber: Fiber<'error, 'value>) : Flow<unit, 'testError, unit> =
        flow {
            if fiber.Metadata.Status = FiberStatus.Running then
                do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 5.0)
                return! waitForSettled fiber
        }

    [<Fact>]
    let ``Observer sees start and end for a joined fiber`` () =
        let recording = Recording()

        let result =
            flow {
                let! fiber = Flow.fork (Flow.succeed 42)
                return! Flow.join fiber
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

        let result =
            flow {
                let! fiber = Flow.fork (Flow.die (InvalidOperationException "silent crash") : Flow<unit, string, int>)
                do! waitForSettled fiber
                return "done"
            }
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()

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
                return! Flow.join fiber
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
    let ``Interrupted fiber ends with interrupted status and no defect`` () =
        let recording = Recording()

        let result =
            flow {
                let! fiber = Flow.fork (Flow.Runtime.sleep (TimeSpan.FromSeconds 30.0) : Flow<unit, string, unit>)
                let! _exit = Flow.interrupt fiber
                return "done"
            }
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()

        test <@ result = Exit.Success "done" @>
        test <@ recording.Ends |> List.map (fun (metadata, defect) -> metadata.Status, defect) = [ FiberStatus.Interrupted, None ] @>
        test <@ recording.Unobserved = [] @>

    [<Fact>]
    let ``Timeout loser defect is reported without fiber metadata`` () =
        let recording = Recording()

        let result =
            dieOnCancel "timeout loser"
            |> Flow.Runtime.timeout (TimeSpan.FromMilliseconds 20.0) "timed out"
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()

        test <@ result = Exit.Failure(Cause.Fail "timed out") @>

        match recording.Unobserved with
        | [ None, defect ] -> test <@ defect.Message = "timeout loser" @>
        | other -> failwithf "Expected one unobserved defect without metadata, got %A" other

    [<Fact>]
    let ``Race loser defect is reported without fiber metadata`` () =
        let recording = Recording()

        let result =
            Flow.race (Flow.succeed ()) (dieOnCancel "race loser" |> Flow.map ignore)
            |> Flow.withFiberObserver recording.Observer
            |> Flow.runSync ()

        test <@ result = Exit.Success () @>

        // The loser settles asynchronously after the race returns.
        waitUntil (fun () -> not (List.isEmpty recording.Unobserved))

        match recording.Unobserved with
        | [ None, defect ] -> test <@ defect.Message = "race loser" @>
        | other -> failwithf "Expected one unobserved defect without metadata, got %A" other

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
                return! Flow.join fiber
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
                    SettledAt = None
                    Status = FiberStatus.Failed
                    Observed = false
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
        let metadata =
            {
                Id = FiberId 1000L
                Name = None
                ParentId = None
                Annotations = Map.empty
                StartedAt = DateTimeOffset.UtcNow
                SettledAt = None
                Status = FiberStatus.Failed
                Observed = false
            }

        let tracker = FiberDefectTracker(metadata, recording.Observer)
        tracker.Settled(Some(InvalidOperationException "boom"))
        metadata.Observed <- true
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
                    SettledAt = None
                    Status = FiberStatus.Failed
                    Observed = false
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
