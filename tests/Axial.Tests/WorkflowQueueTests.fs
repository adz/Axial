namespace Axial.Tests

open System
open Axial
open Swensen.Unquote
open Xunit

module WorkflowQueueTests =
    let private suspendedTakers (queue: Queue<'a>) () =
        Platform.lock queue.Gate (fun () -> queue.Takers.Count)

    let private suspendedOfferers (queue: Queue<'a>) () =
        Platform.lock queue.Gate (fun () -> queue.Offerers.Count)

    /// Polls a condition on queue internals so a test can act once a fiber is known to be suspended.
    let private waitUntil (condition: unit -> bool) : Flow<ClockEnvironment, 'error, unit> =
        let rec loop remaining =
            flow {
                if not (condition ()) && remaining > 0 then
                    do! Flow.sleep (TimeSpan.FromMilliseconds 1.0)
                    return! loop (remaining - 1)
            }

        loop 5000

    let private isInterrupted (exit: Exit<'value, 'error>) =
        match exit with
        | Exit.Failure cause -> Cause.isInterrupted cause
        | Exit.Success _ -> false

    [<Fact>]
    let ``Queue: values leave in the order they were offered`` () =
        let workflow =
            flow {
                let! (queue: Queue<int>) = Queue.bounded 8
                do! queue |> Queue.offerAll [ 1..5 ] |> Flow.ignore
                return! queue |> Dequeue.takeAll
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success [ 1..5 ] @>

    [<Fact>]
    let ``Queue: concurrent producers keep their own order`` () =
        let workflow =
            flow {
                let! (queue: Queue<int * int>) = Queue.bounded 4

                let producer id =
                    flow {
                        for index in 1..100 do
                            do! queue |> Queue.offer (id, index) |> Flow.ignore
                    }

                let! producers = [ producer 1; producer 2; producer 3 ] |> Flow.sequencePar |> Flow.fork
                let! received = List.init 300 (fun _ -> Dequeue.take queue) |> Flow.sequence
                do! Fiber.join producers |> Flow.ignore
                return received
            }

        match Flow.runSync (TestSupport.clockEnv ()) workflow with
        | Exit.Success received ->
            for id in 1..3 do
                let fromProducer = received |> List.filter (fst >> (=) id) |> List.map snd
                test <@ fromProducer = [ 1..100 ] @>
        | other -> failwith $"Expected success, got {other}"

    [<Fact>]
    let ``Queue: each strategy handles a full queue differently`` () =
        let workflow =
            flow {
                let! (dropping: Queue<int>) = Queue.dropping 2
                let! droppedAccepted = dropping |> Queue.offerAll [ 1; 2; 3 ]
                let! droppingContents = dropping |> Dequeue.takeAll

                let! (sliding: Queue<int>) = Queue.sliding 2
                let! slidingAccepted = sliding |> Queue.offerAll [ 1; 2; 3 ]
                let! slidingContents = sliding |> Dequeue.takeAll

                let! (bounded: Queue<int>) = Queue.bounded 2
                do! bounded |> Queue.offerAll [ 1; 2 ] |> Flow.ignore
                let! blocked = bounded |> Queue.offer 3 |> Flow.fork
                do! waitUntil (fun () -> suspendedOfferers bounded () = 1)
                let! stillFull = bounded |> Dequeue.size
                let! _ = Fiber.interrupt blocked

                return droppedAccepted, droppingContents, slidingAccepted, slidingContents, stillFull
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(false, [ 1; 2 ], true, [ 2; 3 ], 2) @>

    [<Fact>]
    let ``Queue: tryOffer reports acceptance, overflow, and shutdown without waiting`` () =
        let workflow =
            flow {
                let! (bounded: Queue<int>) = Queue.bounded 1
                let first = bounded |> Queue.tryOffer 1
                let full = bounded |> Queue.tryOffer 2
                let! boundedValues = Dequeue.takeAll bounded
                do! Dequeue.shutdown bounded
                let shut = bounded |> Queue.tryOffer 3

                let! (dropping: Queue<int>) = Queue.dropping 1
                let _ = dropping |> Queue.tryOffer 1
                let dropped = dropping |> Queue.tryOffer 2
                let! droppedValues = Dequeue.takeAll dropping

                let! (sliding: Queue<int>) = Queue.sliding 1
                let _ = sliding |> Queue.tryOffer 1
                let evicted = sliding |> Queue.tryOffer 2
                let! slidingValues = Dequeue.takeAll sliding

                return first, full, boundedValues, shut, dropped, droppedValues, evicted, slidingValues
            }

        test
            <@ Flow.runSync (TestSupport.clockEnv ()) workflow =
                Exit.Success(
                    QueueTryOfferResult.Accepted,
                    QueueTryOfferResult.Full,
                    [ 1 ],
                    QueueTryOfferResult.Shutdown,
                    QueueTryOfferResult.Dropped,
                    [ 1 ],
                    QueueTryOfferResult.Evicted,
                    [ 2 ]
                ) @>

    [<Fact>]
    let ``Queue: suspended take resumes on offer and suspended offer resumes on take`` () =
        let workflow =
            flow {
                let! (queue: Queue<string>) = Queue.bounded 1
                let! taker = Dequeue.take queue |> Flow.fork
                do! waitUntil (fun () -> suspendedTakers queue () = 1)
                do! queue |> Queue.offer "handed" |> Flow.ignore
                let! taken = Fiber.join taker

                do! queue |> Queue.offer "first" |> Flow.ignore
                let! offerer = queue |> Queue.offer "second" |> Flow.fork
                do! waitUntil (fun () -> suspendedOfferers queue () = 1)
                let! first = Dequeue.take queue
                let! accepted = Fiber.join offerer
                let! second = Dequeue.take queue
                return taken, first, accepted, second
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success("handed", "first", true, "second") @>

    [<Fact>]
    let ``Queue: an interrupted take never loses an element`` () =
        let attempt () =
            flow {
                let! (queue: Queue<int>) = Queue.unbounded ()
                let! taker = Dequeue.take queue |> Flow.fork
                do! waitUntil (fun () -> suspendedTakers queue () = 1)
                // Race the interruption against the offer that would complete the suspended take.
                let! interrupted, _ = Flow.zipPar (Fiber.interrupt taker) (queue |> Queue.offer 7)
                let! remaining = Dequeue.poll queue

                return
                    match interrupted, remaining with
                    | Exit.Success 7, None -> true
                    | exit, Some 7 when isInterrupted exit -> true
                    | _ -> false
            }

        let workflow = List.init 1000 (fun _ -> attempt ()) |> Flow.sequence
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow |> Exit.map (List.forall id) = Exit.Success true @>

    [<Fact>]
    let ``Queue: an interrupted take hands its element to the next suspended taker`` () =
        let attempt () =
            flow {
                let! (queue: Queue<int>) = Queue.unbounded ()
                let! first = Dequeue.take queue |> Flow.fork
                do! waitUntil (fun () -> suspendedTakers queue () = 1)
                let! second = Dequeue.take queue |> Flow.fork
                do! waitUntil (fun () -> suspendedTakers queue () = 2)
                let! firstExit, _ = Flow.zipPar (Fiber.interrupt first) (queue |> Queue.offer 7)

                match firstExit with
                | Exit.Success 7 ->
                    let! secondExit = Fiber.interrupt second
                    return isInterrupted secondExit
                | exit when isInterrupted exit ->
                    // The element went to the first taker in the same instant it was interrupted; it must now
                    // belong to the second taker rather than sit in the buffer or vanish.
                    let! secondValue = Fiber.join second
                    let! remaining = Dequeue.size queue
                    return secondValue = 7 && remaining = 0
                | _ -> return false
            }

        let workflow = List.init 1000 (fun _ -> attempt ()) |> Flow.sequence
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow |> Exit.map (List.forall id) = Exit.Success true @>

    [<Fact>]
    let ``Queue: an interrupted offer never enqueues its value`` () =
        let attempt () =
            flow {
                let! (queue: Queue<int>) = Queue.bounded 1
                do! queue |> Queue.offer 1 |> Flow.ignore
                let! offerer = queue |> Queue.offer 2 |> Flow.fork
                do! waitUntil (fun () -> suspendedOfferers queue () = 1)
                let! interrupted, first = Flow.zipPar (Fiber.interrupt offerer) (Dequeue.take queue)
                let! remaining = Dequeue.takeAll queue

                return
                    first = 1
                    && (match interrupted, remaining with
                        | Exit.Success true, [ 2 ] -> true
                        | exit, [] when isInterrupted exit -> true
                        | _ -> false)
            }

        let workflow = List.init 1000 (fun _ -> attempt ()) |> Flow.sequence
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow |> Exit.map (List.forall id) = Exit.Success true @>

    [<Fact>]
    let ``Queue: suspended takers and offerers are served first come first served`` () =
        let workflow =
            flow {
                let! (queue: Queue<int>) = Queue.bounded 1

                let! takers =
                    [ 1..3 ]
                    |> Flow.traverse (fun index ->
                        flow {
                            let! taker = Dequeue.take queue |> Flow.fork
                            do! waitUntil (fun () -> suspendedTakers queue () = index)
                            return taker
                        })

                do! queue |> Queue.offerAll [ 10; 20; 30 ] |> Flow.ignore
                let! taken = takers |> Flow.traverse Fiber.join

                do! queue |> Queue.offer 0 |> Flow.ignore

                let! offerers =
                    [ 1..3 ]
                    |> Flow.traverse (fun index ->
                        flow {
                            let! offerer = queue |> Queue.offer (index * 100) |> Flow.fork
                            do! waitUntil (fun () -> suspendedOfferers queue () = index)
                            return offerer
                        })

                let! drained = List.init 4 (fun _ -> Dequeue.take queue) |> Flow.sequence
                do! offerers |> Flow.traverse Fiber.join |> Flow.ignore
                return taken, drained
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success([ 10; 20; 30 ], [ 0; 100; 200; 300 ]) @>

    [<Fact>]
    let ``Queue: shutdown interrupts waiters and lets the backlog drain`` () =
        let workflow =
            flow {
                let! (empty: Queue<int>) = Queue.bounded 1
                let! taker = Dequeue.take empty |> Flow.fork
                do! waitUntil (fun () -> suspendedTakers empty () = 1)
                do! Dequeue.shutdown empty
                let! takerExit = Fiber.interrupt taker

                let! (full: Queue<int>) = Queue.bounded 2
                do! full |> Queue.offerAll [ 1; 2 ] |> Flow.ignore
                let! offerer = full |> Queue.offer 3 |> Flow.fork
                do! waitUntil (fun () -> suspendedOfferers full () = 1)
                do! Dequeue.shutdown full
                do! Dequeue.shutdown full
                let! offererExit = Fiber.interrupt offerer
                let! isShut = Dequeue.isShutdown full
                do! Dequeue.awaitShutdown full

                let! first = Dequeue.take full
                let! second = Dequeue.take full
                let! polled = Dequeue.poll full
                let! lateTake = Dequeue.take full |> Flow.fork |> Flow.bind Fiber.interrupt
                let! lateOffer = full |> Queue.offer 4 |> Flow.fork |> Flow.bind Fiber.interrupt

                return
                    isInterrupted takerExit,
                    isInterrupted offererExit,
                    isShut,
                    [ first; second ],
                    polled,
                    isInterrupted lateTake,
                    isInterrupted lateOffer
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(true, true, true, [ 1; 2 ], None, true, true) @>

    [<Fact>]
    let ``Queue: a stream from a queue ends normally after shutdown and drain`` () =
        let workflow =
            flow {
                let! (queue: Queue<int>) = Queue.bounded 2
                let! consumer = queue |> FlowStream.fromDequeue |> FlowStream.runCollect |> Flow.fork
                do! queue |> Queue.offerAll [ 1..5 ] |> Flow.ignore
                do! Dequeue.shutdown queue
                return! Fiber.join consumer
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success [ 1..5 ] @>

    [<Fact>]
    let ``Queue: takeUpTo and takeAll return what is available without suspending`` () =
        let workflow =
            flow {
                let! (queue: Queue<int>) = Queue.unbounded ()
                let! none = queue |> Dequeue.takeUpTo 3
                do! queue |> Queue.offerAll [ 1..5 ] |> Flow.ignore
                let! firstBatch = queue |> Dequeue.takeUpTo 3
                let! rest = queue |> Dequeue.takeAll
                let! afterwards = queue |> Dequeue.takeAll
                return none, firstBatch, rest, afterwards
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success([], [ 1; 2; 3 ], [ 4; 5 ], []) @>

    [<Fact>]
    let ``Queue: takeBetween waits for the minimum and takes up to the maximum`` () =
        let workflow =
            flow {
                let! (queue: Queue<int>) = Queue.unbounded ()
                let! batch = queue |> Dequeue.takeBetween 2 3 |> Flow.fork
                do! queue |> Queue.offer 1 |> Flow.ignore
                do! waitUntil (fun () -> suspendedTakers queue () = 1)
                do! queue |> Queue.offerAll [ 2; 3; 4 ] |> Flow.ignore
                let! first = Fiber.join batch
                let! second = queue |> Dequeue.takeBetween 1 5
                do! queue |> Queue.offer 5 |> Flow.ignore
                do! Dequeue.shutdown queue
                let! remainder = queue |> Dequeue.takeBetween 3 3
                let! afterDrain = queue |> Dequeue.takeBetween 1 1 |> Flow.fork |> Flow.bind Fiber.interrupt
                return first, second, remainder, isInterrupted afterDrain
            }

        // The first batch had 1 and waited for 2; 3 arrived in the same offerAll and fits under the maximum.
        match Flow.runSync (TestSupport.clockEnv ()) workflow with
        | Exit.Success(first, second, remainder, interrupted) ->
            test <@ first @ second = [ 1; 2; 3; 4 ] && first.Length >= 2 && first.Length <= 3 @>
            test <@ remainder = [ 5 ] && interrupted @>
        | other -> failwith $"Expected success, got {other}"

    [<Fact>]
    let ``Queue: an interrupted takeBetween gives its collected values back in order`` () =
        let workflow =
            flow {
                let! (queue: Queue<int>) = Queue.unbounded ()
                do! queue |> Queue.offerAll [ 1; 2 ] |> Flow.ignore
                let! batch = queue |> Dequeue.takeBetween 3 3 |> Flow.fork
                do! waitUntil (fun () -> suspendedTakers queue () = 1)
                let! exit = Fiber.interrupt batch
                do! queue |> Queue.offer 3 |> Flow.ignore
                let! remaining = Dequeue.takeAll queue
                return isInterrupted exit, remaining
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(true, [ 1; 2; 3 ]) @>

    [<Fact>]
    let ``Queue: takeBetween rejects an empty or inverted range`` () =
        let exit =
            flow {
                let! (queue: Queue<int>) = Queue.unbounded ()
                return! queue |> Dequeue.takeBetween 0 1
            }
            |> Flow.runSync (TestSupport.clockEnv ())

        match exit with
        | Exit.Failure(Cause.Die error) -> test <@ error :? ArgumentOutOfRangeException @>
        | other -> failwith $"Expected a defect, got {other}"

    [<Fact>]
    let ``Queue: stats count accepted, dropped, and evicted values`` () =
        let workflow =
            flow {
                let! (dropping: Queue<int>) = Queue.dropping 2
                do! dropping |> Queue.offerAll [ 1..5 ] |> Flow.ignore
                let! droppingStats = Dequeue.stats dropping

                let! (sliding: Queue<int>) = Queue.sliding 2
                do! sliding |> Queue.offerAll [ 1..5 ] |> Flow.ignore
                let! slidingStats = Dequeue.stats sliding

                let! (bounded: Queue<int>) = Queue.bounded 1
                do! bounded |> Queue.offer 1 |> Flow.ignore
                let! blocked = bounded |> Queue.offer 2 |> Flow.fork
                do! waitUntil (fun () -> suspendedOfferers bounded () = 1)
                let! waiting = Dequeue.stats bounded
                let! _ = Dequeue.take bounded
                do! Fiber.join blocked |> Flow.ignore
                do! Dequeue.shutdown bounded
                let! settled = Dequeue.stats bounded

                return droppingStats, slidingStats, waiting, settled
            }

        match Flow.runSync (TestSupport.clockEnv ()) workflow with
        | Exit.Success(dropping, sliding, waiting, settled) ->
            test <@ (dropping.Accepted, dropping.Dropped, dropping.Evicted, dropping.Size) = (2L, 3L, 0L, 2) @>
            test <@ (sliding.Accepted, sliding.Dropped, sliding.Evicted, sliding.Size) = (5L, 0L, 3L, 2) @>
            test <@ (waiting.Accepted, waiting.WaitingOfferers, waiting.Capacity) = (1L, 1, Some 1) @>
            test <@ (settled.Accepted, settled.WaitingOfferers, settled.Size, settled.IsShutdown) = (2L, 0, 1, true) @>
        | other -> failwith $"Expected success, got {other}"

    [<Fact>]
    let ``Queue: a scoped queue shuts down when its scope closes`` () =
        let workflow =
            flow {
                let! queue, consumer =
                    flow {
                        let! (queue: Queue<int>) = Queue.makeScoped (QueueStrategy.BackPressure 4)
                        do! queue |> Queue.offerAll [ 1; 2 ] |> Flow.ignore
                        return queue
                    }
                    |> Flow.scoped
                    |> Flow.bind (fun queue ->
                        queue |> FlowStream.fromDequeue |> FlowStream.runCollect |> Flow.map (fun values -> queue, values))

                let! isShut = Dequeue.isShutdown queue
                return isShut, consumer
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(true, [ 1; 2 ]) @>

    [<Fact>]
    let ``Queue: a non-positive capacity is a defect`` () =
        let exit = (Queue.bounded 0 : Flow<ClockEnvironment, unit, Queue<int>>) |> Flow.runSync (TestSupport.clockEnv ())

        match exit with
        | Exit.Failure(Cause.Die error) -> test <@ error :? ArgumentOutOfRangeException @>
        | other -> failwith $"Expected a defect, got {other}"
