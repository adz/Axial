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
    let private waitUntil (condition: unit -> bool) : Flow<unit, 'error, unit> =
        let rec loop remaining =
            flow {
                if not (condition ()) && remaining > 0 then
                    do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 1.0)
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
                return! queue |> Queue.takeAll
            }

        test <@ Flow.runSync () workflow = Exit.Success [ 1..5 ] @>

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
                let! received = List.init 300 (fun _ -> Queue.take queue) |> Flow.sequence
                do! Flow.join producers |> Flow.ignore
                return received
            }

        match Flow.runSync () workflow with
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
                let! droppingContents = dropping |> Queue.takeAll

                let! (sliding: Queue<int>) = Queue.sliding 2
                let! slidingAccepted = sliding |> Queue.offerAll [ 1; 2; 3 ]
                let! slidingContents = sliding |> Queue.takeAll

                let! (bounded: Queue<int>) = Queue.bounded 2
                do! bounded |> Queue.offerAll [ 1; 2 ] |> Flow.ignore
                let! blocked = bounded |> Queue.offer 3 |> Flow.fork
                do! waitUntil (fun () -> suspendedOfferers bounded () = 1)
                let! stillFull = bounded |> Queue.size
                let! _ = Flow.interrupt blocked

                return droppedAccepted, droppingContents, slidingAccepted, slidingContents, stillFull
            }

        test <@ Flow.runSync () workflow = Exit.Success(false, [ 1; 2 ], true, [ 2; 3 ], 2) @>

    [<Fact>]
    let ``Queue: suspended take resumes on offer and suspended offer resumes on take`` () =
        let workflow =
            flow {
                let! (queue: Queue<string>) = Queue.bounded 1
                let! taker = Queue.take queue |> Flow.fork
                do! waitUntil (fun () -> suspendedTakers queue () = 1)
                do! queue |> Queue.offer "handed" |> Flow.ignore
                let! taken = Flow.join taker

                do! queue |> Queue.offer "first" |> Flow.ignore
                let! offerer = queue |> Queue.offer "second" |> Flow.fork
                do! waitUntil (fun () -> suspendedOfferers queue () = 1)
                let! first = Queue.take queue
                let! accepted = Flow.join offerer
                let! second = Queue.take queue
                return taken, first, accepted, second
            }

        test <@ Flow.runSync () workflow = Exit.Success("handed", "first", true, "second") @>

    [<Fact>]
    let ``Queue: an interrupted take never loses an element`` () =
        let attempt () =
            flow {
                let! (queue: Queue<int>) = Queue.unbounded ()
                let! taker = Queue.take queue |> Flow.fork
                do! waitUntil (fun () -> suspendedTakers queue () = 1)
                // Race the interruption against the offer that would complete the suspended take.
                let! interrupted, _ = Flow.zipPar (Flow.interrupt taker) (queue |> Queue.offer 7)
                let! remaining = Queue.poll queue

                return
                    match interrupted, remaining with
                    | Exit.Success 7, None -> true
                    | exit, Some 7 when isInterrupted exit -> true
                    | _ -> false
            }

        let workflow = List.init 1000 (fun _ -> attempt ()) |> Flow.sequence
        test <@ Flow.runSync () workflow |> Exit.map (List.forall id) = Exit.Success true @>

    [<Fact>]
    let ``Queue: an interrupted take hands its element to the next suspended taker`` () =
        let attempt () =
            flow {
                let! (queue: Queue<int>) = Queue.unbounded ()
                let! first = Queue.take queue |> Flow.fork
                do! waitUntil (fun () -> suspendedTakers queue () = 1)
                let! second = Queue.take queue |> Flow.fork
                do! waitUntil (fun () -> suspendedTakers queue () = 2)
                let! firstExit, _ = Flow.zipPar (Flow.interrupt first) (queue |> Queue.offer 7)

                match firstExit with
                | Exit.Success 7 ->
                    let! secondExit = Flow.interrupt second
                    return isInterrupted secondExit
                | exit when isInterrupted exit ->
                    // The element went to the first taker in the same instant it was interrupted; it must now
                    // belong to the second taker rather than sit in the buffer or vanish.
                    let! secondValue = Flow.join second
                    let! remaining = Queue.size queue
                    return secondValue = 7 && remaining = 0
                | _ -> return false
            }

        let workflow = List.init 1000 (fun _ -> attempt ()) |> Flow.sequence
        test <@ Flow.runSync () workflow |> Exit.map (List.forall id) = Exit.Success true @>

    [<Fact>]
    let ``Queue: an interrupted offer never enqueues its value`` () =
        let attempt () =
            flow {
                let! (queue: Queue<int>) = Queue.bounded 1
                do! queue |> Queue.offer 1 |> Flow.ignore
                let! offerer = queue |> Queue.offer 2 |> Flow.fork
                do! waitUntil (fun () -> suspendedOfferers queue () = 1)
                let! interrupted, first = Flow.zipPar (Flow.interrupt offerer) (Queue.take queue)
                let! remaining = Queue.takeAll queue

                return
                    first = 1
                    && (match interrupted, remaining with
                        | Exit.Success true, [ 2 ] -> true
                        | exit, [] when isInterrupted exit -> true
                        | _ -> false)
            }

        let workflow = List.init 1000 (fun _ -> attempt ()) |> Flow.sequence
        test <@ Flow.runSync () workflow |> Exit.map (List.forall id) = Exit.Success true @>

    [<Fact>]
    let ``Queue: suspended takers and offerers are served first come first served`` () =
        let workflow =
            flow {
                let! (queue: Queue<int>) = Queue.bounded 1

                let! takers =
                    [ 1..3 ]
                    |> Flow.traverse (fun index ->
                        flow {
                            let! taker = Queue.take queue |> Flow.fork
                            do! waitUntil (fun () -> suspendedTakers queue () = index)
                            return taker
                        })

                do! queue |> Queue.offerAll [ 10; 20; 30 ] |> Flow.ignore
                let! taken = takers |> Flow.traverse Flow.join

                do! queue |> Queue.offer 0 |> Flow.ignore

                let! offerers =
                    [ 1..3 ]
                    |> Flow.traverse (fun index ->
                        flow {
                            let! offerer = queue |> Queue.offer (index * 100) |> Flow.fork
                            do! waitUntil (fun () -> suspendedOfferers queue () = index)
                            return offerer
                        })

                let! drained = List.init 4 (fun _ -> Queue.take queue) |> Flow.sequence
                do! offerers |> Flow.traverse Flow.join |> Flow.ignore
                return taken, drained
            }

        test <@ Flow.runSync () workflow = Exit.Success([ 10; 20; 30 ], [ 0; 100; 200; 300 ]) @>

    [<Fact>]
    let ``Queue: shutdown interrupts waiters and lets the backlog drain`` () =
        let workflow =
            flow {
                let! (empty: Queue<int>) = Queue.bounded 1
                let! taker = Queue.take empty |> Flow.fork
                do! waitUntil (fun () -> suspendedTakers empty () = 1)
                do! Queue.shutdown empty
                let! takerExit = Flow.interrupt taker

                let! (full: Queue<int>) = Queue.bounded 2
                do! full |> Queue.offerAll [ 1; 2 ] |> Flow.ignore
                let! offerer = full |> Queue.offer 3 |> Flow.fork
                do! waitUntil (fun () -> suspendedOfferers full () = 1)
                do! Queue.shutdown full
                do! Queue.shutdown full
                let! offererExit = Flow.interrupt offerer
                let! isShut = Queue.isShutdown full
                do! Queue.awaitShutdown full

                let! first = Queue.take full
                let! second = Queue.take full
                let! polled = Queue.poll full
                let! lateTake = Queue.take full |> Flow.fork |> Flow.bind Flow.interrupt
                let! lateOffer = full |> Queue.offer 4 |> Flow.fork |> Flow.bind Flow.interrupt

                return
                    isInterrupted takerExit,
                    isInterrupted offererExit,
                    isShut,
                    [ first; second ],
                    polled,
                    isInterrupted lateTake,
                    isInterrupted lateOffer
            }

        test <@ Flow.runSync () workflow = Exit.Success(true, true, true, [ 1; 2 ], None, true, true) @>

    [<Fact>]
    let ``Queue: a stream from a queue ends normally after shutdown and drain`` () =
        let workflow =
            flow {
                let! (queue: Queue<int>) = Queue.bounded 2
                let! consumer = queue |> FlowStream.fromQueue |> FlowStream.runCollect |> Flow.fork
                do! queue |> Queue.offerAll [ 1..5 ] |> Flow.ignore
                do! Queue.shutdown queue
                return! Flow.join consumer
            }

        test <@ Flow.runSync () workflow = Exit.Success [ 1..5 ] @>

    [<Fact>]
    let ``Queue: takeUpTo and takeAll return what is available without suspending`` () =
        let workflow =
            flow {
                let! (queue: Queue<int>) = Queue.unbounded ()
                let! none = queue |> Queue.takeUpTo 3
                do! queue |> Queue.offerAll [ 1..5 ] |> Flow.ignore
                let! firstBatch = queue |> Queue.takeUpTo 3
                let! rest = queue |> Queue.takeAll
                let! afterwards = queue |> Queue.takeAll
                return none, firstBatch, rest, afterwards
            }

        test <@ Flow.runSync () workflow = Exit.Success([], [ 1; 2; 3 ], [ 4; 5 ], []) @>

    [<Fact>]
    let ``Queue: a scoped queue shuts down when its scope closes`` () =
        let workflow =
            flow {
                let! queue, consumer =
                    flow {
                        let! (queue: Queue<int>) = Queue.boundedScoped 4
                        do! queue |> Queue.offerAll [ 1; 2 ] |> Flow.ignore
                        return queue
                    }
                    |> Flow.scoped
                    |> Flow.bind (fun queue ->
                        queue |> FlowStream.fromQueue |> FlowStream.runCollect |> Flow.map (fun values -> queue, values))

                let! isShut = Queue.isShutdown queue
                return isShut, consumer
            }

        test <@ Flow.runSync () workflow = Exit.Success(true, [ 1; 2 ]) @>

    [<Fact>]
    let ``Queue: a non-positive capacity is a defect`` () =
        let exit = (Queue.bounded 0 : Flow<unit, unit, Queue<int>>) |> Flow.runSync ()

        match exit with
        | Exit.Failure(Cause.Die error) -> test <@ error :? ArgumentOutOfRangeException @>
        | other -> failwith $"Expected a defect, got {other}"
