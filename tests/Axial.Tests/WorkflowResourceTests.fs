namespace Axial.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open Axial.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowResourceTests =
    let private scopeUse acquire release useResource =
        Flow.scopeAcquireRelease acquire release
        |> Flow.bind useResource
        |> Flow.scoped

    type TrackingDisposable(events: ResizeArray<string>, name: string) =
        member _.Name = name

        interface IDisposable with
            member _.Dispose() =
                events.Add($"{name}:disposed")

    [<Fact>]
    let ``flow use covers lexical cleanup`` () =
        let events = ResizeArray<string>()

        let workflow =
            flow {
                use resource = new TrackingDisposable(events, "resource")
                events.Add($"{resource.Name}:used")
                return resource.Name
            }

        let result = Flow.runSync () workflow

        test <@ result = Exit.Success "resource" @>
        test <@ List.ofSeq events = [ "resource:used"; "resource:disposed" ] @>

    [<Fact>]
    let ``Flow scoped acquisition releases after typed failure`` () =
        let releaseCount = ref 0

        let acquireReleaseResult =
            scopeUse
                (Flow.succeed 7)
                (fun _ _ ->
                    releaseCount.Value <- releaseCount.Value + 1
                    Task.CompletedTask)
                (fun _ -> Flow.fail "boom")
            |> Flow.runSync ()

        test <@ acquireReleaseResult = Exit.Failure (Cause.Fail "boom") @>
        test <@ releaseCount.Value = 1 @>

    [<Fact>]
    let ``Flow scoped acquisition releases after defects`` () =
        let releaseCount = ref 0

        let acquireReleaseResult =
            scopeUse
                (Flow.succeed 7)
                (fun _ _ ->
                    releaseCount.Value <- releaseCount.Value + 1
                    Task.CompletedTask)
                (fun _ -> Flow.die (InvalidOperationException "boom"))
            |> Flow.runSync ()

        match acquireReleaseResult with
        | Exit.Failure (Cause.Die error) -> test <@ error.Message = "boom" @>
        | other -> failwithf "Expected defect, got %A" other
        test <@ releaseCount.Value = 1 @>

    [<Fact>]
    let ``Flow scoped acquisition releases after interruption`` () =
        let releaseCount = ref 0
        let interrupted = Flow(fun _ _ -> Execution.ofInterrupt ())

        let acquireReleaseResult =
            scopeUse
                (Flow.succeed 7)
                (fun _ _ ->
                    releaseCount.Value <- releaseCount.Value + 1
                    Task.CompletedTask)
                (fun _ -> interrupted)
            |> Flow.runSync ()

        test <@ acquireReleaseResult = Exit.Failure Cause.Interrupt @>
        test <@ releaseCount.Value = 1 @>

    [<Fact>]
    let ``Flow scoped acquisition releases once when release defects`` () =
        let releaseCount = ref 0

        let acquireReleaseResult =
            scopeUse
                (Flow.succeed 7)
                (fun _ _ ->
                    releaseCount.Value <- releaseCount.Value + 1
                    Task.FromException(InvalidOperationException "release failed"))
                (fun _ -> Flow.succeed "ok")
            |> Flow.runSync ()

        match acquireReleaseResult with
        | Exit.Failure (Cause.Die error) -> test <@ error.Message = "release failed" @>
        | other -> failwithf "Expected release defect, got %A" other

        test <@ releaseCount.Value = 1 @>

    [<Fact>]
    let ``Flow scope functions register disposable and resource finalizer`` () =
        let events = ResizeArray<string>()

        let workflow =
            Flow.scoped (
                flow {
                    let disposable = new TrackingDisposable(events, "disposable")
                    do! Flow.scopeDisposable disposable
                    do!
                        Resource.finalizer(fun _ -> events.Add("finalized"); Task.CompletedTask)
                        |> Flow.scopeResource
                        |> Flow.ignore
                    events.Add("used")
                })

        let result = Flow.runSync () workflow

        test <@ result = Exit.Success () @>
        test <@ List.ofSeq events = [ "used"; "finalized"; "disposable:disposed" ] @>

    [<Fact>]
    let ``Flow scopeAcquireRelease keeps resource alive until runtime scope closes`` () =
        let events = ResizeArray<string>()

        let workflow =
            flow {
                let! resource =
                    Flow.scopeAcquireRelease
                        (Flow.succeed "resource")
                        (fun name _ ->
                            events.Add($"{name}:released")
                            Task.CompletedTask)

                events.Add($"{resource}:after-acquire")

                do!
                    flow {
                        events.Add($"{resource}:subflow")
                    }

                events.Add($"{resource}:after-subflow")
                return resource
            }

        let result = Flow.runSync () workflow

        test <@ result = Exit.Success "resource" @>
        test <@ List.ofSeq events = [ "resource:after-acquire"; "resource:subflow"; "resource:after-subflow"; "resource:released" ] @>

    [<Fact>]
    let ``Flow scoped finalizers close in deterministic reverse order`` () =
        let events = ResizeArray<string>()

        let workflow =
            flow {
                do! Flow.scopeFinalizer(fun _ ->
                    events.Add "first"
                    Task.CompletedTask)

                do! Flow.scopeFinalizer(fun _ ->
                    events.Add "second"
                    Task.CompletedTask)
            }

        let result = Flow.runSync () workflow

        test <@ result = Exit.Success () @>
        test <@ List.ofSeq events = [ "second"; "first" ] @>

    [<Fact>]
    let ``Layer acquireRelease releases when provided flow completes`` () =
        let events = ResizeArray<string>()

        let layer =
            Layer.acquireRelease
                (Layer.succeed "service")
                (fun service _ ->
                    events.Add($"{service}:released")
                    Task.CompletedTask)

        let workflow =
            flow {
                let! service = Flow.env<string, string>
                events.Add($"{service}:used")
                return service
            }

        let result =
            workflow
            |> Layer.provide layer
            |> Flow.runSync ()

        test <@ result = Exit.Success "service" @>
        test <@ List.ofSeq events = [ "service:used"; "service:released" ] @>

    [<Fact>]
    let ``Layer acquireRelease releases successful branch when parallel sibling fails`` () =
        let events = ResizeArray<string>()

        let successful =
            Layer.acquireRelease
                (Layer.succeed "service")
                (fun service _ ->
                    events.Add($"{service}:released")
                    Task.CompletedTask)

        let failed =
            Layer.fromValueTask (fun (_, _) _ -> Execution.ofError "failed")

        let result =
            Flow.env<string * string, string>
            |> Layer.provide (Layer.merge successful failed)
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Fail "failed") @>
        test <@ List.ofSeq events = [ "service:released" ] @>

    [<Fact>]
    let ``Layer pool hands out instances round-robin`` () =
        let workflow =
            flow {
                let! pool = Flow.env<Pool<int>, string>
                return [ for _ in 1 .. 5 -> pool.Next() ]
            }

        let result =
            workflow
            |> Layer.provide (Layer.pool 3 Layer.succeed)
            |> Flow.runSync ()

        test <@ result = Exit.Success [ 0; 1; 2; 0; 1 ] @>

    [<Fact>]
    let ``Layer pool acquires instances sequentially in index order`` () =
        let events = ResizeArray<int>()

        let layer =
            Layer.pool 3 (fun index ->
                events.Add index
                Layer.succeed index)

        let result =
            (Flow.env<Pool<int>, string> |> Flow.map (fun pool -> pool.Count))
            |> Layer.provide layer
            |> Flow.runSync ()

        test <@ result = Exit.Success 3 @>
        test <@ List.ofSeq events = [ 0; 1; 2 ] @>

    [<Fact>]
    let ``Layer pool releases every acquired instance when a later instance fails`` () =
        let events = ResizeArray<string>()

        let layer =
            Layer.pool 3 (fun index ->
                if index = 2 then
                    Layer.fromValueTask (fun (_, _) _ -> Execution.ofError "boom")
                else
                    Layer.acquireRelease
                        (Layer.succeed index)
                        (fun instance _ ->
                            events.Add($"{instance}:released")
                            Task.CompletedTask))

        let result =
            Flow.env<Pool<int>, string>
            |> Layer.provide layer
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Fail "boom") @>
        test <@ List.ofSeq events = [ "1:released"; "0:released" ] @>

    [<Fact>]
    let ``Layer pool rejects a non-positive size`` () =
        raises<ArgumentException> <@ Layer.pool 0 Layer.succeed @>

    // Reads how many finalizers the running flow's scope still holds.
    let private registeredInCurrentScope () : Flow<unit, 'error, int> =
        Flow(fun _ _ -> Execution.ofValue (RuntimeState.current().Scope.RegisteredCount))

    [<Fact>]
    let ``A closed child scope no longer occupies its parent scope`` () =
        let workflow =
            flow {
                for _ in 1..1000 do
                    do! Flow.scoped (Flow.succeed ())

                return! registeredInCurrentScope ()
            }
            |> Flow.scoped

        test <@ Flow.runSync () workflow = Exit.Success 0 @>

    [<Fact>]
    let ``A settled fiber no longer occupies the scope that forked it`` () =
        let workflow =
            flow {
                for index in 1..200 do
                    let! fiber =
                        if index % 2 = 0 then Flow.succeed index else Flow.fail "expected"
                        |> Flow.fork

                    let! _ = Fiber.interrupt fiber
                    ()

                let! gate = Deferred.make<unit, string, unit> ()
                let! running = Flow.fork (Deferred.await gate)
                let! whileRunning = registeredInCurrentScope ()
                let! _ = Fiber.interrupt running
                let! afterSettle = registeredInCurrentScope ()
                return whileRunning, afterSettle
            }
            |> Flow.scoped

        // A running fiber holds two registrations, its interruption and its own scope; settling releases both.
        test <@ Flow.runSync () workflow = Exit.Success(2, 0) @>

    [<Fact>]
    let ``A forked fiber releases what it acquired when it settles, not when its parent scope closes`` () =
        let workflow =
            flow {
                let events = ResizeArray<string>()

                let! fiber =
                    flow {
                        do! Flow.scopeFinalizer (fun _ -> task { lock events (fun () -> events.Add "released") })
                        return "done"
                    }
                    |> Flow.fork

                let! _ = Fiber.join fiber
                let releasedBeforeParentCloses = lock events (fun () -> List.ofSeq events)
                return releasedBeforeParentCloses
            }
            |> Flow.scoped

        test <@ Flow.runSync () workflow = Exit.Success [ "released" ] @>

    [<Fact>]
    let ``A forked consumer's hub subscription ends with the consumer`` () =
        let workflow : Flow<unit, Never, int * PublishResult> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! subscribed = Deferred.make<unit, Never, unit> ()

                let! consumer =
                    flow {
                        let! subscription = hub |> Hub.subscribe (QueueStrategy.BackPressure 1)
                        do! Deferred.succeed () subscribed |> Flow.ignore
                        return! Dequeue.take subscription
                    }
                    |> Flow.fork

                do! Deferred.await subscribed
                do! hub |> Hub.publish 1 |> Flow.ignore
                let! _ = Fiber.join consumer
                let! remaining = Hub.subscriberCount hub
                // Without the fiber's own scope the ended consumer's full subscription would hold this publish forever.
                let blocked = { PublishResult.empty with Delivered = -1 }
                let! later = hub |> Hub.publishAll [ 2; 3; 4 ] |> Flow.timeoutToOk (TimeSpan.FromSeconds 5.0) blocked
                return remaining, later
            }

        test <@ Flow.runSync () workflow = Exit.Success(0, PublishResult.empty) @>

    [<Fact>]
    let ``forkGraceful lets a consumer drain its queue when the scope closes`` () =
        let workflow : Flow<unit, Never, int> =
            flow {
                let flushed = ref 0

                do!
                    flow {
                        let! (samples: Queue<int>) = Queue.bounded 100

                        let! _ =
                            samples
                            |> FlowStream.fromDequeue
                            |> FlowStream.tapFlow (fun _ -> Flow.sleep (TimeSpan.FromMilliseconds 1.0))
                            |> FlowStream.runForEach (fun _ -> flushed.Value <- flushed.Value + 1)
                            |> Flow.forkGraceful (Dequeue.shutdown samples) (TimeSpan.FromSeconds 10.0)

                        do! samples |> Queue.offerAll [ 1..50 ] |> Flow.ignore
                    }
                    |> Flow.scoped

                return flushed.Value
            }

        test <@ Flow.runSync () workflow = Exit.Success 50 @>

    [<Fact>]
    let ``forkGraceful interrupts a fiber that ignores its stop after the grace period`` () =
        let workflow : Flow<unit, Never, bool * bool> =
            flow {
                let interrupted = ref false
                let started = Diagnostics.Stopwatch.StartNew()

                do!
                    flow {
                        let! _ =
                            Flow.sleep (TimeSpan.FromMinutes 5.0)
                            |> Flow.onInterrupt (Flow.delay (fun () -> interrupted.Value <- true; Flow.ok ()))
                            |> Flow.forkGraceful (Flow.ok ()) (TimeSpan.FromMilliseconds 50.0)

                        ()
                    }
                    |> Flow.scoped

                return interrupted.Value, started.Elapsed < TimeSpan.FromSeconds 30.0
            }

        test <@ Flow.runSync () workflow = Exit.Success(true, true) @>

    [<Fact>]
    let ``forkGraceful drains even when the forking flow is interrupted`` () =
        let workflow : Flow<unit, Never, int> =
            flow {
                let flushed = ref 0
                let! ready = Deferred.make<unit, Never, unit> ()

                let! owner =
                    flow {
                        let! (samples: Queue<int>) = Queue.unbounded ()

                        let! _ =
                            samples
                            |> FlowStream.fromDequeue
                            |> FlowStream.runForEach (fun _ -> flushed.Value <- flushed.Value + 1)
                            |> Flow.forkGraceful (Dequeue.shutdown samples) (TimeSpan.FromSeconds 10.0)

                        do! samples |> Queue.offerAll [ 1..20 ] |> Flow.ignore
                        do! Deferred.succeed () ready |> Flow.ignore
                        do! Flow.never
                    }
                    |> Flow.scoped
                    |> Flow.fork

                do! Deferred.await ready
                let! _ = Fiber.interrupt owner
                return flushed.Value
            }

        test <@ Flow.runSync () workflow = Exit.Success 20 @>

    [<Fact>]
    let ``ensuring runs its finalizer after every kind of outcome and keeps the outcome`` () =
        let runs = ref 0
        let finalizer : Flow<unit, Never, unit> = Flow.delay (fun () -> runs.Value <- runs.Value + 1; Flow.ok ())
        let boom = InvalidOperationException "boom"

        let succeeded = Flow.ok 1 |> Flow.ensuring finalizer |> Flow.runSync ()
        let failed : Exit<int, string> = Flow.fail "expected" |> Flow.ensuring finalizer |> Flow.runSync ()
        let died : Exit<int, string> = Flow.die boom |> Flow.ensuring finalizer |> Flow.runSync ()

        let interrupted : Exit<unit, string> =
            flow {
                let! fiber = Flow.never<unit, string, unit> |> Flow.ensuring finalizer |> Flow.fork
                return! Fiber.interrupt fiber
            }
            |> Flow.runSync ()
            |> Exit.bind id

        test <@ succeeded = Exit.Success 1 && failed = Exit.Failure(Cause.Fail "expected") @>
        test <@ died = Exit.Failure(Cause.Die boom) @>
        test <@ (match interrupted with Exit.Failure cause -> Cause.isInterrupted cause | _ -> false) @>
        test <@ runs.Value = 4 @>

    [<Fact>]
    let ``onExit sees the outcome, and a defect in the handler is added to it`` () =
        let seen = ResizeArray<string>()

        let record (exit: Exit<int, string>) : Flow<unit, Never, unit> =
            Flow.delay (fun () ->
                seen.Add(match exit with Exit.Success value -> $"success {value}" | Exit.Failure _ -> "failure")
                Flow.ok ())

        let succeeded = Flow.ok 2 |> Flow.onExit record |> Flow.runSync ()
        let failed = Flow.fail "expected" |> Flow.onExit record |> Flow.runSync ()
        let broken = InvalidOperationException "cleanup failed"
        let brokenHandler : Flow<unit, Never, unit> = Flow.die broken
        let successThenDefect : Exit<int, string> = Flow.ok 3 |> Flow.ensuring brokenHandler |> Flow.runSync ()
        let failureThenDefect : Exit<int, string> = Flow.fail "expected" |> Flow.ensuring brokenHandler |> Flow.runSync ()

        test <@ succeeded = Exit.Success 2 && failed = Exit.Failure(Cause.Fail "expected") @>
        test <@ List.ofSeq seen = [ "success 2"; "failure" ] @>
        test <@ successThenDefect = Exit.Failure(Cause.Die broken) @>
        test <@ failureThenDefect = Exit.Failure(Cause.Then(Cause.Fail "expected", Cause.Die broken)) @>

    [<Fact>]
    let ``onInterrupt runs only on interruption, and runs to completion despite it`` () =
        let cleaned = ref 0

        // The handler itself waits; it must still finish, because it runs without the interrupted flow's cancellation.
        let cleanup : Flow<unit, Never, unit> =
            flow {
                do! Flow.sleep (TimeSpan.FromMilliseconds 20.0)
                cleaned.Value <- cleaned.Value + 1
            }

        let completed = Flow.ok "done" |> Flow.onInterrupt cleanup |> Flow.runSync ()
        let failed : Exit<string, string> = Flow.fail "expected" |> Flow.onInterrupt cleanup |> Flow.runSync ()

        let interrupted =
            flow {
                let! fiber = Flow.never<unit, Never, unit> |> Flow.onInterrupt cleanup |> Flow.fork
                return! Fiber.interrupt fiber
            }
            |> Flow.runSync ()

        test <@ completed = Exit.Success "done" && failed = Exit.Failure(Cause.Fail "expected") @>
        test <@ (match interrupted with Exit.Success(Exit.Failure cause) -> Cause.isInterrupted cause | _ -> false) @>
        test <@ cleaned.Value = 1 @>

