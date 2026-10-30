namespace Axial.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
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
                (fun _ -> Flow.delay (fun () -> releaseCount.Value <- releaseCount.Value + 1; Flow.succeed ()))
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
                (fun _ -> Flow.delay (fun () -> releaseCount.Value <- releaseCount.Value + 1; Flow.succeed ()))
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
                (fun _ -> Flow.delay (fun () -> releaseCount.Value <- releaseCount.Value + 1; Flow.succeed ()))
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
                (fun _ ->
                    Flow.delay (fun () ->
                        releaseCount.Value <- releaseCount.Value + 1
                        Flow.die (InvalidOperationException "release failed")))
                (fun _ -> Flow.succeed "ok")
            |> Flow.runSync ()

        match acquireReleaseResult with
        | Exit.Failure (Cause.Die error) -> test <@ error.Message = "release failed" @>
        | other -> failwithf "Expected release defect, got %A" other

        test <@ releaseCount.Value = 1 @>

    [<Fact>]
    let ``A release runs without interruption when cancellation closes its scope`` () =
        use cts = new CancellationTokenSource()
        let events = ResizeArray<string>()

        let workflow : Flow<unit, string, unit> =
            flow {
                do!
                    Flow.scopeFinalizer (
                        flow {
                            // Interruptible work: it would stop at once if the release saw the cancellation.
                            do! Flow.sleep (TimeSpan.FromMilliseconds 20.0)
                            let! token = Flow.cancellationToken
                            lock events (fun () -> events.Add $"released:{token.IsCancellationRequested}")
                        }
                    )

                lock events (fun () -> events.Add "acquired")
                do! Flow.sleep (TimeSpan.FromSeconds 30.0)
            }

        cts.CancelAfter(TimeSpan.FromMilliseconds 50.0)
        let result = Flow.runSyncWithToken () cts.Token workflow

        test <@ result = Exit.Failure Cause.Interrupt @>
        test <@ List.ofSeq events = [ "acquired"; "released:False" ] @>

    [<Fact>]
    let ``Cancellation during a gated release waits for uncancelled completion`` () =
        use cts = new CancellationTokenSource()
        let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let gate = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let completed = ref false
        let tokenCancelled = ref true

        let workflow : Flow<unit, string, unit> =
            flow {
                do! Flow.scopeFinalizer (flow {
                    entered.TrySetResult () |> ignore
                    do! Flow.fromTask (fun _ -> gate.Task)
                    let! token = Flow.cancellationToken
                    tokenCancelled.Value <- token.IsCancellationRequested
                    completed.Value <- true
                })
            }

        let running = Task.Run(fun () -> Flow.runSyncWithToken () cts.Token workflow)
        Assert.True(entered.Task.Wait(TimeSpan.FromSeconds 10.0))
        cts.Cancel()
        gate.TrySetResult () |> ignore
        let result = running.GetAwaiter().GetResult()
        test <@ result = Exit.Success () @>
        test <@ completed.Value && not tokenCancelled.Value @>

    [<Fact>]
    let ``Nested gated release finishes under cancellation and then closes its resource`` () =
        use cts = new CancellationTokenSource()
        let entered = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let gate = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let events = ResizeArray<string>()

        let workflow : Flow<unit, string, unit> =
            flow {
                do! Flow.scopeFinalizer (flow {
                    let! _ = Flow.scopeAcquireRelease (Flow.succeed "inner") (fun _ ->
                        Flow.delay (fun () -> events.Add "inner released"; Flow.succeed ()))
                    entered.TrySetResult () |> ignore
                    do! Flow.fromTask (fun _ -> gate.Task)
                    events.Add "outer finished"
                })
            }

        let running = Task.Run(fun () -> Flow.runSyncWithToken () cts.Token workflow)
        Assert.True(entered.Task.Wait(TimeSpan.FromSeconds 10.0))
        cts.Cancel()
        gate.TrySetResult () |> ignore
        test <@ running.GetAwaiter().GetResult() = Exit.Success () @>
        test <@ List.ofSeq events = [ "outer finished"; "inner released" ] @>

    [<Fact>]
    let ``All finalizers run in reverse order when the middle finalizer dies`` () =
        let events = ResizeArray<string>()
        let defect = InvalidOperationException "middle"
        let release name = Flow.delay (fun () -> events.Add name; Flow.succeed ())
        let workflow : Flow<unit, string, unit> =
            flow {
                do! Flow.scopeFinalizer (release "first")
                do! Flow.scopeFinalizer (Flow.delay (fun () -> events.Add "middle"; Flow.die defect))
                do! Flow.scopeFinalizer (release "third")
            }
        test <@ Flow.runSync () workflow = Exit.Failure(Cause.Die defect) @>
        test <@ List.ofSeq events = [ "third"; "middle"; "first" ] @>

    [<Fact>]
    let ``Synchronous release construction defect does not skip other finalizers`` () =
        let events = ResizeArray<string>()
        let defect = InvalidOperationException "construct release"
        let workflow : Flow<unit, string, unit> =
            flow {
                do! Flow.scopeFinalizer (Flow.delay (fun () -> events.Add "first"; Flow.succeed ()))
                let! _ = Flow.scopeAcquireRelease (Flow.succeed "resource") (fun _ -> raise defect)
                do! Flow.scopeFinalizer (Flow.delay (fun () -> events.Add "last"; Flow.succeed ()))
            }
        test <@ Flow.runSync () workflow = Exit.Failure(Cause.Die defect) @>
        test <@ List.ofSeq events = [ "last"; "first" ] @>

    [<Fact>]
    let ``Nested scope defect followed by parent release defect preserves ordered cause`` () =
        let inner = InvalidOperationException "inner"
        let outer = InvalidOperationException "outer"
        let workflow : Flow<unit, string, unit> =
            flow {
                do! Flow.scopeFinalizer (Flow.die outer)
                do! Flow.scoped (Flow.scopeFinalizer (Flow.die inner))
            }
        test <@ Flow.runSync () workflow = Exit.Failure(Cause.Then(Cause.Die inner, Cause.Die outer)) @>

    [<Fact>]
    let ``A release sees the environment captured at acquisition`` () =
        let seen = ResizeArray<string>()

        let workflow : Flow<string, string, string> =
            flow {
                let! resource =
                    Flow.scopeAcquireRelease
                        (Flow.envWith (fun (env: string) -> $"connection:{env}"))
                        (fun connection -> Flow.envWith (fun (env: string) -> seen.Add $"{connection} released in {env}"))
                    |> Flow.localEnv (fun (_: string) -> "tenant-a")

                return resource
            }
            |> Flow.scoped

        let result = Flow.runSync "root" workflow

        test <@ result = Exit.Success "connection:tenant-a" @>
        test <@ List.ofSeq seen = [ "connection:tenant-a released in tenant-a" ] @>

    [<Fact>]
    let ``A release that throws is a defect and the scope's other releases still run`` () =
        let events = ResizeArray<string>()
        let releaseDefect = InvalidOperationException "release threw"

        let workflow : Flow<unit, string, unit> =
            flow {
                do! Flow.scopeFinalizer (Flow.delay (fun () -> events.Add "first released"; Flow.succeed ()))
                do! Flow.scopeFinalizer (Flow.delay (fun () -> raise releaseDefect))
            }
            |> Flow.scoped

        let result = Flow.runSync () workflow

        test <@ result = Exit.Failure(Cause.Die releaseDefect) @>
        test <@ List.ofSeq events = [ "first released" ] @>

    [<Fact>]
    let ``A release closes the resources it acquires itself`` () =
        let events = ResizeArray<string>()

        let workflow : Flow<unit, string, unit> =
            Flow.scopeFinalizer (
                flow {
                    do! Flow.scopeFinalizer (Flow.delay (fun () -> events.Add "inner released"; Flow.succeed ()))
                    events.Add "outer released"
                }
            )
            |> Flow.scoped

        let result = Flow.runSync () workflow

        test <@ result = Exit.Success () @>
        test <@ List.ofSeq events = [ "outer released"; "inner released" ] @>

    [<Fact>]
    let ``Flow scope functions register disposable and resource finalizer`` () =
        let events = ResizeArray<string>()

        let workflow =
            Flow.scoped (
                flow {
                    let disposable = new TrackingDisposable(events, "disposable")
                    do! Flow.scopeDisposable disposable
                    do!
                        Resource.finalizer (Flow.delay (fun () -> events.Add("finalized"); Flow.succeed ()))
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
                        (fun name -> Flow.delay (fun () -> events.Add($"{name}:released"); Flow.succeed ()))

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
                do! Flow.scopeFinalizer (Flow.delay (fun () -> events.Add "first"; Flow.succeed ()))

                do! Flow.scopeFinalizer (Flow.delay (fun () -> events.Add "second"; Flow.succeed ()))
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
                (fun service -> Flow.delay (fun () -> events.Add($"{service}:released"); Flow.succeed ()))

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
                (fun service -> Flow.delay (fun () -> events.Add($"{service}:released"); Flow.succeed ()))

        let failed =
            Layer.fromFlow (Flow.fail "failed")

        let result =
            Flow.env<string * string, string>
            |> Layer.provide (Layer.merge successful failed)
            |> Flow.runSync ()

        test <@ result = Exit.Failure (Cause.Fail "failed") @>
        test <@ List.ofSeq events = [ "service:released" ] @>

    [<Fact>]
    let ``Layer fromFlow keeps scoped resources until the provided flow completes and releases them in reverse`` () =
        let events = ResizeArray<string>()

        let open' (name: string) =
            Flow.scopeAcquireRelease
                (Flow.envWith (fun (region: string) -> events.Add $"{name}:acquired"; $"{name}@{region}"))
                (fun resource -> Flow.envWith (fun (region: string) -> events.Add $"{resource}:released in {region}"))

        let layer : Layer<string, string, string * string> =
            Layer.fromFlow (
                flow {
                    let! database = open' "database"
                    let! cache = open' "cache"
                    return database, cache
                }
            )

        let workflow =
            flow {
                let! database, cache = Flow.env<string * string, string>
                events.Add $"used {database} {cache}"
                return database
            }

        let result = workflow |> Layer.provide layer |> Flow.runSync "eu"

        test <@ result = Exit.Success "database@eu" @>

        test
            <@
                List.ofSeq events = [ "database:acquired"
                                      "cache:acquired"
                                      "used database@eu cache@eu"
                                      "cache@eu:released in eu"
                                      "database@eu:released in eu" ]
            @>

    [<Fact>]
    let ``Layer fromFlow releases what it acquired when provisioning fails`` () =
        let events = ResizeArray<string>()

        let layer : Layer<unit, string, string> =
            Layer.fromFlow (
                flow {
                    let! _ =
                        Flow.scopeAcquireRelease
                            (Flow.succeed "connection")
                            (fun resource -> Flow.delay (fun () -> events.Add $"{resource}:released"; Flow.succeed ()))

                    return! Flow.fail "migration failed"
                }
            )

        let result =
            Flow.delay (fun () -> events.Add "used"; Flow.succeed ())
            |> Layer.provide layer
            |> Flow.runSync ()

        test <@ result = Exit.Failure(Cause.Fail "migration failed") @>
        test <@ List.ofSeq events = [ "connection:released" ] @>

    [<Fact>]
    let ``Layer addFinalizer runs its release with the layer input as environment`` () =
        let events = ResizeArray<string>()

        let layer : Layer<string, string, int> =
            Layer.addFinalizer (Flow.envWith (fun (input: string) -> events.Add $"released with {input}"))
            |> Layer.bind (fun () -> Layer.envWith String.length)

        let result = Flow.env<int, string> |> Layer.provide layer |> Flow.runSync "input"

        test <@ result = Exit.Success 5 @>
        test <@ List.ofSeq events = [ "released with input" ] @>

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
                    Layer.fromFlow (Flow.fail "boom")
                else
                    Layer.acquireRelease
                        (Layer.succeed index)
                        (fun instance -> Flow.delay (fun () -> events.Add($"{instance}:released"); Flow.succeed ())))

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

        // Supervision is the running fiber's only parent registration; settling releases it.
        test <@ Flow.runSync () workflow = Exit.Success(1, 0) @>

    [<Fact>]
    let ``A forked fiber releases what it acquired when it settles, not when its parent scope closes`` () =
        let workflow =
            flow {
                let events = ResizeArray<string>()

                let! fiber =
                    flow {
                        do! Flow.scopeFinalizer (Flow.delay (fun () -> lock events (fun () -> events.Add "released"); Flow.succeed ()))
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

    [<Fact>]
    let ``A layer that cannot fail combines with a layer that can`` () =
        let cannotFail : Layer<unit, Never, int> = Layer.succeed 20
        let canFail : Layer<unit, string, int> = Layer.succeed 22
        let combined = Layer.map2 (+) (Layer.widenError cannotFail) canFail
        let failing = Layer.map2 (+) (Layer.widenError cannotFail) (Layer.fromFlow (Flow.fail "down"))

        test <@ Flow.envWith id |> Layer.provide combined |> Flow.runSync () = Exit.Success 42 @>
        test <@ (Flow.envWith id : Flow<int, string, int>) |> Layer.provide failing |> Flow.runSync () = Exit.Failure(Cause.Fail "down") @>
