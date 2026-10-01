namespace Axial.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowConcurrencyTests =
    [<Fact>]
    let ``Deferred: await receives successful completion`` () =
        let workflow : Flow<ClockEnvironment, string, bool * int> =
            flow {
                let! deferred = Deferred.make<ClockEnvironment, string, int> ()
                let! fiber = Deferred.await deferred |> Flow.fork
                let! completed = Deferred.succeed 42 deferred
                let! value = Fiber.join fiber
                return completed, value
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(true, 42) @>

    [<Fact>]
    let ``Deferred: completion is idempotent`` () =
        let workflow : Flow<ClockEnvironment, string, bool * bool * int> =
            flow {
                let! deferred = Deferred.make<ClockEnvironment, string, int> ()
                let! first = Deferred.succeed 1 deferred
                let! second = Deferred.fail "late" deferred
                let! value = Deferred.await deferred
                return first, second, value
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(true, false, 1) @>

    [<Fact>]
    let ``Deferred: direct completion reports the winner to a synchronous callback`` () =
        let workflow : Flow<ClockEnvironment, string, bool * bool * int> =
            flow {
                let! deferred = Deferred.make<ClockEnvironment, string, int> ()
                let first = Deferred.succeedNow 42 deferred
                let late = Deferred.failNow "late" deferred
                let! value = Deferred.await deferred
                return first, late, value
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(true, false, 42) @>

    [<Fact>]
    let ``Deferred: direct completion preserves every Exit shape`` () =
        let defect = InvalidOperationException("defect")

        let setup : Flow<ClockEnvironment, string, Deferred<string, int> list> =
            flow {
                let! completed = Deferred.make<ClockEnvironment, string, int> ()
                let! failed = Deferred.make<ClockEnvironment, string, int> ()
                let! died = Deferred.make<ClockEnvironment, string, int> ()
                let! interrupted = Deferred.make<ClockEnvironment, string, int> ()
                return [ completed; failed; died; interrupted ]
            }

        match Flow.runSync (TestSupport.clockEnv ()) setup with
        | Exit.Success [ completed; failed; died; interrupted ] ->
            test <@ Deferred.completeNow (Exit.Success 1) completed @>
            test <@ Deferred.failNow "failure" failed @>
            test <@ Deferred.dieNow defect died @>
            test <@ Deferred.interruptNow interrupted @>
            test <@ Flow.runSync (TestSupport.clockEnv ()) (Deferred.await completed) = Exit.Success 1 @>
            test <@ Flow.runSync (TestSupport.clockEnv ()) (Deferred.await failed) = Exit.Failure(Cause.Fail "failure") @>
            test <@ Flow.runSync (TestSupport.clockEnv ()) (Deferred.await died) = Exit.Failure(Cause.Die defect) @>
            test <@ Flow.runSync (TestSupport.clockEnv ()) (Deferred.await interrupted) = Exit.Failure Cause.Interrupt @>
        | other -> failwith $"Expected four deferreds, got {other}"

    [<Fact>]
    let ``Deferred: await preserves typed failure defect and interruption`` () =
        let typedFailure : Flow<ClockEnvironment, string, int> =
            flow {
                let! deferred = Deferred.make<ClockEnvironment, string, int> ()
                let! _ = Deferred.fail "boom" deferred
                return! Deferred.await deferred
            }

        let defect = InvalidOperationException("defect")

        let defective : Flow<ClockEnvironment, string, int> =
            flow {
                let! deferred = Deferred.make<ClockEnvironment, string, int> ()
                let! _ = Deferred.die defect deferred
                return! Deferred.await deferred
            }

        let interrupted : Flow<ClockEnvironment, string, int> =
            flow {
                let! deferred = Deferred.make<ClockEnvironment, string, int> ()
                let! _ = Deferred.interrupt deferred
                return! Deferred.await deferred
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) typedFailure = Exit.Failure(Cause.Fail "boom") @>
        test <@ Flow.runSync (TestSupport.clockEnv ()) defective = Exit.Failure(Cause.Die defect) @>
        test <@ Flow.runSync (TestSupport.clockEnv ()) interrupted = Exit.Failure Cause.Interrupt @>

    [<Fact>]
    let ``Deferred: await respects runtime cancellation`` () =
        use cts = new CancellationTokenSource()

        let workflow : Flow<ClockEnvironment, string, int> =
            flow {
                let! deferred = Deferred.make<ClockEnvironment, string, int> ()
                return! Deferred.await deferred
            }

        cts.Cancel()

        test <@ Flow.runSyncWithToken (TestSupport.clockEnv ()) cts.Token workflow = Exit.Failure Cause.Interrupt @>

    [<Fact>]
    let ``Deferred: await composes with race`` () =
        let workflow : Flow<ClockEnvironment, string, int> =
            flow {
                let! deferred = Deferred.make<ClockEnvironment, string, int> ()
                let! _ = Deferred.succeed 7 deferred

                return!
                    Flow.race
                        (Deferred.await deferred)
                        (flow {
                            do! Flow.sleep (TimeSpan.FromMilliseconds 100.0)
                            return 99
                        })
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success 7 @>

    [<Fact>]
    let ``Semaphore: withPermit serializes concurrent sections`` () =
        let gate = obj()
        let mutable active = 0
        let mutable maxActive = 0

        let enter () =
            lock gate (fun () ->
                active <- active + 1
                maxActive <- max maxActive active)

        let leave () =
            lock gate (fun () -> active <- active - 1)

        let workflow : Flow<ClockEnvironment, string, int> =
            flow {
                let! semaphore = Semaphore.make 1

                let protectedFlow =
                    Semaphore.withPermit semaphore (
                        flow {
                            enter ()
                            do! Flow.sleep (TimeSpan.FromMilliseconds 40.0)
                            leave ()
                        })

                let! left = Flow.fork protectedFlow
                let! right = Flow.fork protectedFlow
                do! Fiber.join left
                do! Fiber.join right
                return maxActive
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success 1 @>

    [<Fact>]
    let ``Semaphore: withPermit releases after typed failure and defect`` () =
        let semaphore =
            match Flow.runSync (TestSupport.clockEnv ()) (Semaphore.make 1) with
            | Exit.Success semaphore -> semaphore
            | other -> failwithf "Expected semaphore creation, got %A" other

        let afterTypedFailure : Flow<ClockEnvironment, string, string> =
            Semaphore.withPermit semaphore (Flow.fail "boom")
            |> Flow.orElseWith (fun error ->
                flow {
                    let! next = Semaphore.withPermit semaphore (Flow.succeed "next")
                    return $"{error}:{next}"
                })

        let defect = InvalidOperationException("defect")
        let afterDefect = Semaphore.withPermit semaphore (Flow.die defect)
        let afterDefectRelease = Semaphore.withPermit semaphore (Flow.succeed "released")

        test <@ Flow.runSync (TestSupport.clockEnv ()) afterTypedFailure = Exit.Success "boom:next" @>
        test <@ Flow.runSync (TestSupport.clockEnv ()) afterDefect = Exit.Failure(Cause.Die defect) @>
        test <@ Flow.runSync (TestSupport.clockEnv ()) afterDefectRelease = Exit.Success "released" @>

    [<Fact>]
    let ``Semaphore: make rejects non-positive permit counts`` () =
        let result : Exit<FlowSemaphore, string> =
            Flow.runSync (TestSupport.clockEnv ()) (Semaphore.make 0)

        match result with
        | Exit.Failure(Cause.Die(:? ArgumentOutOfRangeException as error)) ->
            test <@ error.ParamName = "permits" @>
        | other ->
            failwithf "Expected permit count defect, got %A" other

    [<Fact>]
    let ``Fiber: fork and join success`` () =
        let workflow : Flow<ClockEnvironment, string, int> =
            flow {
                let! (fiber: Fiber<string, int>) = Flow.ok 42 |> Flow.fork
                let! result = fiber |> Fiber.join
                return result
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success 42 @>

    [<Fact>]
    let ``Fiber: fork and join failure`` () =
        let workflow : Flow<ClockEnvironment, string, int> =
            flow {
                let! (fiber: Fiber<string, int>) = Flow.fail "boom" |> Flow.fork
                let! result = fiber |> Fiber.join
                return result
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Failure (Cause.Fail "boom") @>

    [<Fact>]
    let ``Fiber: interrupt stops execution`` () =
        let mutable executed = false
        let workflow =
            flow {
                let! (fiber: Fiber<string, int>) = 
                    flow {
                        do! Flow.sleep (TimeSpan.FromMilliseconds 500.0)
                        executed <- true
                        return 42
                    }
                    |> Flow.fork
                
                do! Flow.sleep (TimeSpan.FromMilliseconds 100.0)
                let! exit = fiber |> Fiber.interrupt
                return exit
            }

        let outcome = Flow.runSync (TestSupport.clockEnv ()) workflow
        
        match outcome with
        | Exit.Success (Exit.Failure Cause.Interrupt) -> 
            test <@ executed = false @>
        | _ -> failwithf "Expected interrupted exit, got %A" outcome

    [<Fact>]
    let ``Semaphore: an interrupted waiter does not lose the permit`` () =
        let waitingCount (FlowSemaphore queue) () =
            Platform.lock queue.Gate (fun () -> queue.Waiters.Count)

        let attempt () : Flow<ClockEnvironment, string, bool> =
            flow {
                let! semaphore = Semaphore.make 1
                let! release = Deferred.make<ClockEnvironment, string, unit> ()
                let! holder = Semaphore.withPermit semaphore (Deferred.await release) |> Flow.fork
                let! waiter = Semaphore.withPermit semaphore (Flow.succeed ()) |> Flow.fork

                let rec untilQueued remaining =
                    flow {
                        if waitingCount semaphore () = 0 && remaining > 0 then
                            do! Flow.sleep (TimeSpan.FromMilliseconds 1.0)
                            return! untilQueued (remaining - 1)
                    }

                do! untilQueued 5000
                // Race the interruption against the release that would grant the waiter its permit.
                let! _ = Flow.zipPar (Fiber.interrupt waiter) (Deferred.succeed () release)
                do! Fiber.join holder
                // The permit must be available again whichever side of the race won.
                return! Semaphore.withPermit semaphore (Flow.succeed true) |> Flow.timeoutToOk (TimeSpan.FromSeconds 5.0) false
            }

        let workflow = List.init 200 (fun _ -> attempt ()) |> Flow.sequence
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow |> Exit.map (List.forall id) = Exit.Success true @>

    [<Fact>]
    let ``Deferred: each run of make creates a new deferred`` () =
        let make = Deferred.make<ClockEnvironment, string, int> ()

        let workflow : Flow<ClockEnvironment, string, bool * bool> =
            flow {
                let! first = make
                let! second = make
                let! completedFirst = Deferred.succeed 1 first
                let! completedSecond = Deferred.succeed 2 second
                return completedFirst, completedSecond
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(true, true) @>

    [<Fact>]
    let ``Fiber.await returns the exit without failing`` () =
        let workflow : Flow<ClockEnvironment, string, Exit<int, string> * Exit<int, string>> =
            flow {
                let! failing = Flow.fork (Flow.fail "boom")
                let! succeeding = Flow.fork (Flow.ok 7)
                let! failed = Fiber.await failing
                let! succeeded = Fiber.await succeeding
                return failed, succeeded
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(Exit.Failure(Cause.Fail "boom"), Exit.Success 7) @>

    [<Fact>]
    let ``Fiber.poll reports None until the fiber settles`` () =
        let workflow : Flow<ClockEnvironment, string, Exit<unit, string> option * Exit<unit, string> option> =
            flow {
                let! gate = Deferred.make<ClockEnvironment, string, unit> ()
                let! fiber = Flow.fork (Deferred.await gate)
                let! before = Fiber.poll fiber
                let! _ = Deferred.succeed () gate
                let! _ = Fiber.await fiber
                let! after = Fiber.poll fiber
                return before, after
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(None, Some(Exit.Success())) @>

    [<Fact>]
    let ``Fiber.interrupt returns the interrupted exit`` () =
        let workflow : Flow<ClockEnvironment, string, Exit<unit, string>> =
            flow {
                let! fiber = Flow.fork (Flow.sleep (TimeSpan.FromMinutes 1.0))
                return! Fiber.interrupt fiber
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(Exit.Failure Cause.Interrupt) @>

    [<Fact>]
    let ``catchCancellation converts only cancellation the runtime did not request`` () =
        let selfCanceled : Exit<unit, string> =
            Flow.fromTask (fun _ -> Task.FromCanceled<unit>(CancellationToken(true)))
            |> Flow.catchCancellation (fun _ -> "library timeout")
            |> Flow.runSync (TestSupport.clockEnv ())

        use cts = new CancellationTokenSource(TimeSpan.FromMilliseconds 20.0)

        let requested : Exit<unit, string> =
            Flow.fromTask (fun token -> task { do! Task.Delay(Timeout.Infinite, token) })
            |> Flow.catchCancellation (fun _ -> "library timeout")
            |> Flow.runSyncWithToken (TestSupport.clockEnv ()) cts.Token

        test <@ selfCanceled = Exit.Failure(Cause.Fail "library timeout") @>
        test <@ requested = Exit.Failure Cause.Interrupt @>

    [<Fact>]
    let ``Blocking constructors run off the calling thread and classify failures`` () =
        let callerThread = Thread.CurrentThread.ManagedThreadId
        let blockingThread = ref callerThread

        let value =
            Flow.fromBlocking (fun _ ->
                blockingThread.Value <- Thread.CurrentThread.ManagedThreadId
                Thread.Sleep 10
                42)
            |> Flow.runSync (TestSupport.clockEnv ())

        let result : Exit<int, string> = Flow.fromBlockingResult (fun _ -> Error "rejected") |> Flow.runSync (TestSupport.clockEnv ())
        let defect : Exit<int, string> = Flow.fromBlocking (fun _ -> failwith "boom") |> Flow.runSync (TestSupport.clockEnv ())
        let attempted = Flow.attemptBlocking (fun _ -> failwith "boom") |> Flow.runSync (TestSupport.clockEnv ())

        test <@ value = Exit.Success 42 @>
        test <@ blockingThread.Value <> callerThread @>
        test <@ result = Exit.Failure(Cause.Fail "rejected") @>
        test <@ match defect with Exit.Failure(Cause.Die _) -> true | _ -> false @>
        test <@ match attempted with Exit.Failure(Cause.Fail (:? Exception)) -> true | _ -> false @>

    [<Fact>]
    let ``Blocking work runs to completion when interrupted and reports the interruption`` () =
        use cts = new CancellationTokenSource(TimeSpan.FromMilliseconds 20.0)
        let finished = ref false

        let exit : Exit<unit, string> =
            Flow.fromBlocking (fun token ->
                while not token.IsCancellationRequested do
                    Thread.Sleep 5

                finished.Value <- true
                token.ThrowIfCancellationRequested())
            |> Flow.runSyncWithToken (TestSupport.clockEnv ()) cts.Token

        test <@ exit = Exit.Failure Cause.Interrupt @>
        test <@ finished.Value @>

    [<Fact>]
    let ``Parallelism.ofProcessors is never below one`` () =
        test <@ Parallelism.value (Parallelism.ofProcessors (fun _ -> 0)) = 1 @>
        test <@ Parallelism.value (Parallelism.ofProcessors id) = Environment.ProcessorCount @>

    [<Fact>]
    let ``forkReplacing interrupts the previous fiber and keeps the latest`` () =
        let workflow : Flow<ClockEnvironment, string, Exit<string, string> * Exit<string, string>> =
            flow {
                let! slot = FiberSlot.make ()
                let! first = Flow.sleep (TimeSpan.FromSeconds 30.0) |> Flow.map (fun () -> "first") |> Flow.forkReplacing slot
                let! second = Flow.ok "second" |> Flow.forkReplacing slot
                let! firstExit = Fiber.await first
                let! secondExit = Fiber.await second
                return firstExit, secondExit
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(Exit.Failure Cause.Interrupt, Exit.Success "second") @>

    [<Fact>]
    let ``forkReplacingKey replaces per key and forgets settled keys`` () =
        let workflow : Flow<ClockEnvironment, string, Exit<int, string> * Exit<int, string> * int * int> =
            flow {
                let! slots = FiberSlot.makeKeyed ()
                let slowly value = Flow.sleep (TimeSpan.FromMilliseconds 50.0) |> Flow.map (fun () -> value)
                let! a1 = Flow.sleep (TimeSpan.FromSeconds 30.0) |> Flow.map (fun () -> 1) |> Flow.forkReplacingKey "a" slots
                let! a2 = slowly 2 |> Flow.forkReplacingKey "a" slots
                let! _ = slowly 3 |> Flow.forkReplacingKey "b" slots
                let! running = FiberSlot.count slots
                let! a1Exit = Fiber.await a1
                let! a2Exit = Fiber.await a2
                do! Flow.sleep (TimeSpan.FromMilliseconds 100.0)
                let! remaining = FiberSlot.count slots
                return a1Exit, a2Exit, running, remaining
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(Exit.Failure Cause.Interrupt, Exit.Success 2, 2, 0) @>

    [<Fact>]
    let ``FiberSlot.interrupt stops the current fiber and waits for its cleanup`` () =
        let cleaned = ref false

        let workflow : Flow<ClockEnvironment, string, bool> =
            flow {
                let! slot = FiberSlot.make ()

                let! _ =
                    Flow.sleep (TimeSpan.FromSeconds 30.0)
                    |> Flow.fold Flow.ok (fun cause ->
                        cleaned.Value <- true
                        Flow.ofExit (Exit.Failure cause))
                    |> Flow.forkReplacing slot

                do! Flow.sleep (TimeSpan.FromMilliseconds 10.0)
                do! FiberSlot.interrupt slot
                return cleaned.Value
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success true @>
