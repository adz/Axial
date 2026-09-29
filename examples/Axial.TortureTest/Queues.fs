/// Many producers and consumers on one queue, with takes and offers interrupted at random, then shutdown mid-stream.
module Axial.TortureTest.Queues

open Axial
open Axial.TortureTest.Scenario

// <snippet:torture-queues>
let run (round: Round) : Flow<unit, Never, Check list> =
    let producers = 4
    let perProducer = round.Size 300
    let consumers = 3

    flow {
        let consumed = ResizeArray<int * int>()
        let seenBy = Array.init consumers (fun _ -> ResizeArray<int * int>())
        let stolen = ResizeArray<int * int>()
        let offered = ResizeArray<int * int>()
        let withdrawn = ResizeArray<int * int>()
        let! (queue: Queue<int * int>) = Queue.make (QueueStrategy.BackPressure 8)

        // Producers alternate single offers and batches. Every value they offer is accepted: the queue is lossless.
        let producer id =
            flow {
                let mutable next = 1

                while next <= perProducer do
                    if round.Chance 30 then
                        let batch = [ for sample in next .. min perProducer (next + 4) -> id, sample ]
                        let! _ = queue |> Queue.offerAll batch
                        lock offered (fun () -> offered.AddRange batch)
                        next <- next + batch.Length
                    else
                        let! _ = queue |> Queue.offer (id, next)
                        lock offered (fun () -> offered.Add(id, next))
                        next <- next + 1
            }

        // Consumers mix every way of taking, until the queue is shut down and drained.
        let consumer index =
            flow {
                let mutable running = true

                while running do
                    let! batch =
                        match round.Next 4 with
                        | 0 -> queue |> Dequeue.take |> Flow.map List.singleton |> exitOf
                        | 1 -> queue |> Dequeue.takeBetween 1 6 |> exitOf
                        | 2 -> queue |> Dequeue.poll |> Flow.map Option.toList |> exitOf
                        | _ -> queue |> Dequeue.takeUpTo 3 |> exitOf

                    match batch with
                    | Exit.Success values ->
                        lock consumed (fun () -> consumed.AddRange values)
                        seenBy[index].AddRange values
                    | Exit.Failure _ -> running <- false
            }

        // Takes interrupted as they start: a value handed over in the same instant is either kept by the take
        // (the interruption reports success) or given back to the front of the queue.
        let saboteur =
            flow {
                for _ in 1 .. round.Size 200 do
                    let! taker = queue |> Dequeue.take |> Flow.fork
                    let! exit = Fiber.interrupt taker

                    match exit with
                    | Exit.Success value -> lock stolen (fun () -> stolen.Add value)
                    | Exit.Failure _ -> ()
            }

        // Offers interrupted as they start: an offer interrupted before it was accepted must never enqueue.
        let withdrawer =
            flow {
                for sample in 1 .. round.Size 100 do
                    let! offer = queue |> Queue.offer (0, sample) |> Flow.fork
                    let! exit = Fiber.interrupt offer

                    match exit with
                    | Exit.Success _ -> lock offered (fun () -> offered.Add(0, sample))
                    | Exit.Failure _ -> lock withdrawn (fun () -> withdrawn.Add(0, sample))
            }

        let! consumerFibers = [ 0 .. consumers - 1 ] |> Flow.traverse (consumer >> Flow.fork)
        do! [ for id in 1..producers -> producer id ] @ [ saboteur; withdrawer ] |> Flow.sequencePar |> Flow.ignore
        let! capacity = Flow.ok (Dequeue.capacity queue)
        do! Dequeue.shutdown queue
        do! Dequeue.awaitShutdown queue
        do! consumerFibers |> Flow.traverse Fiber.join |> Flow.ignore
        let! leftover = Dequeue.takeAll queue
        let! stats = Dequeue.stats queue
        let! isShut = Dequeue.isShutdown queue
        let! size = Dequeue.size queue

        let everything = List.ofSeq consumed @ List.ofSeq stolen @ leftover

        return
            [ check "every accepted value is taken exactly once" (List.sort everything = List.sort (List.ofSeq offered))
              check "no withdrawn offer was ever enqueued" (withdrawn |> Seq.forall (fun value -> not (List.contains value everything)))
              check
                  "each consumer saw every producer's values in order"
                  (seenBy |> Array.forall (List.ofSeq >> List.filter (fst >> (<>) 0) >> inOrderPerProducer))
              check "the queue counted every accepted value" (stats.Accepted = int64 offered.Count)
              check "no taker or offerer is left waiting" (stats.WaitingTakers = 0 && stats.WaitingOfferers = 0)
              check "the queue is shut down and empty" (isShut && size = 0 && capacity = Some 8) ]
    }

/// Lossy strategies under concurrent producers: a dropping queue and a sliding queue each read by a slow consumer.
let runLossy (round: Round) : Flow<unit, Never, Check list> =
    let perProducer = round.Size 500

    let exercise (make: Flow<unit, Never, Queue<int * int>>) =
        flow {
            let! queue = make
            let seen = ResizeArray<int * int>()
            let accepted = ref 0

            let! reader =
                flow {
                    let mutable running = true

                    while running do
                        let! next = queue |> Dequeue.take |> exitOf

                        match next with
                        | Exit.Success value -> seen.Add value
                        | Exit.Failure _ -> running <- false
                }
                |> Flow.fork

            do!
                [ for id in 1..3 ->
                      flow {
                          for sample in 1..perProducer do
                              let! wasAccepted = queue |> Queue.offer (id, sample)
                              if wasAccepted then lock accepted (fun () -> accepted.Value <- accepted.Value + 1)
                      } ]
                |> Flow.sequencePar
                |> Flow.ignore

            do! Dequeue.shutdown queue
            do! Fiber.join reader
            let! stats = Dequeue.stats queue
            return List.ofSeq seen, accepted.Value, stats
        }

    flow {
        let! dropSeen, dropAccepted, dropStats = exercise (Queue.dropping 4)
        let! slideSeen, slideAccepted, slideStats = exercise (Queue.sliding 4)
        let offered = 3 * perProducer

        // A queue created under a scope is shut down when the scope closes.
        let! scoped = Queue.makeScoped (QueueStrategy.Unbounded) |> Flow.scoped
        let! scopedShut = Dequeue.isShutdown scoped
        let! (unbounded: Queue<int>) = Queue.unbounded ()
        do! unbounded |> Queue.offerAll [ 1..1000 ] |> Flow.ignore
        let! unboundedAll = Dequeue.takeAll unbounded
        let! (callbackInbox: Queue<int>) = Queue.bounded 1
        let first = callbackInbox |> Queue.tryOffer 7
        let full = callbackInbox |> Queue.tryOffer 8
        let! accepted = Dequeue.take callbackInbox
        do! Dequeue.shutdown callbackInbox
        let shut = callbackInbox |> Queue.tryOffer 9

        return
            [ check "a dropping queue accounts for every offer as accepted or dropped" (int64 dropAccepted + dropStats.Dropped = int64 offered)
              check "a dropping queue delivers what it accepted, in each producer's order" (dropSeen.Length = dropAccepted && inOrderPerProducer dropSeen)
              check "a sliding queue accepts every offer" (slideAccepted = offered)
              check "a sliding queue delivers or evicts every value, in each producer's order" (int64 slideSeen.Length + slideStats.Evicted = int64 offered && inOrderPerProducer slideSeen)
              check "a scoped queue is shut down with its scope" scopedShut
              check "a synchronous callback sees acceptance, full, and shutdown without losing the accepted value"
                  (first = QueueTryOfferResult.Accepted && full = QueueTryOfferResult.Full && accepted = 7
                   && shut = QueueTryOfferResult.Shutdown)
              check "an unbounded queue never refuses" (unboundedAll = [ 1..1000 ]) ]
    }
// </snippet:torture-queues>

let scenario : Scenario =
    { Name = "queues"
      Title = "Queue contention: interrupted takes and offers, every strategy, shutdown mid-stream"
      Run = fun round -> Flow.map2 (@) (run round) (runLossy round) }
