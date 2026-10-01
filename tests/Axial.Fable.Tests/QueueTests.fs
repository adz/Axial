/// Queues, hubs, SubscriptionRef, and the streams built on them, on the JavaScript runtime.
module Axial.Fable.Tests.QueueTests

open System
open Axial
open Axial.State
open Axial.Fable.Tests.Harness

let private isInterrupted (exit: Exit<'value, 'error>) =
    match exit with
    | Exit.Failure cause -> Cause.isInterrupted cause
    | Exit.Success _ -> false

let tests : Test list =
    [ test "Each queue strategy handles a full queue its own way" (flow {
          let! (dropping: Queue<int>) = Queue.dropping 2
          let! droppingAccepted = dropping |> Queue.offerAll [ 1..4 ]
          let! droppingKept = Dequeue.takeAll dropping
          let! (sliding: Queue<int>) = Queue.sliding 2
          do! sliding |> Queue.offerAll [ 1..4 ] |> Flow.ignore
          let! slidingKept = Dequeue.takeAll sliding
          let! slidingStats = Dequeue.stats sliding

          return
              [ equal "dropping reports the discard" false droppingAccepted
                equal "dropping keeps the oldest" [ 1; 2 ] droppingKept
                equal "sliding keeps the newest" [ 3; 4 ] slidingKept
                equal "sliding counts evictions" 2L slidingStats.Evicted ]
      })

      test "An interrupted take never loses a value or reorders the queue" (flow {
          let results = ResizeArray<bool>()

          for _ in 1..200 do
              let! (queue: Queue<int>) = Queue.unbounded ()
              let! first = Dequeue.take queue |> Flow.fork
              let! second = Dequeue.take queue |> Flow.fork
              let! firstExit, _ = Flow.zipPar (Fiber.interrupt first) (queue |> Queue.offerAll [ 1; 2 ])
              let! secondValue = Fiber.join second
              let! rest = Dequeue.takeAll queue

              // Whoever was interrupted, 1 is taken before 2 and neither is lost.
              let taken =
                  match firstExit with
                  | Exit.Success value -> [ value; secondValue ] @ rest
                  | _ -> secondValue :: rest

              results.Add((taken = [ 1; 2 ]))

          return [ isTrue "every round kept both values in order" (Seq.forall id results) ]
      })

      test "takeBetween waits for its minimum and shutdown drains the backlog" (flow {
          let! (queue: Queue<int>) = Queue.bounded 8
          let! batch = queue |> Dequeue.takeBetween 3 5 |> Flow.fork
          do! queue |> Queue.offerAll [ 1; 2; 3; 4 ] |> Flow.ignore
          let! first = Fiber.join batch
          do! queue |> Queue.offer 5 |> Flow.ignore
          do! Dequeue.shutdown queue
          let! remainder = queue |> Dequeue.takeBetween 3 3
          let! afterDrain = Dequeue.take queue |> exitOf

          return
              [ isTrue "first batch has three or four values in order" (first = [ 1; 2; 3 ] || first = [ 1; 2; 3; 4 ])
                equal "the remainder after shutdown" (if first.Length = 3 then [ 4; 5 ] else [ 5 ]) remainder
                isTrue "a take after the drain is interrupted" (isInterrupted afterDrain) ]
      })

      test "A hub feeds lossless, sliding, and dropping subscribers" (flow {
          let! (hub: Hub<int>) = Hub.make ()
          let! historian = hub |> Hub.subscribe (QueueStrategy.BackPressure 2)
          let! display = hub |> Hub.subscribe (QueueStrategy.Sliding 1)
          let! alarms = hub |> Hub.subscribe (QueueStrategy.Dropping 1)
          let! recorded = historian |> FlowStream.fromDequeue |> FlowStream.runCollect |> Flow.fork
          do! hub |> Hub.publishAll [ 1..6 ] |> Flow.ignore
          do! Hub.shutdown hub
          let! history = Fiber.join recorded
          let! latest = Dequeue.takeAll display
          let! firstAlarm = Dequeue.takeAll alarms
          let! (tryHub: Hub<int>) = Hub.make ()
          let! _full = tryHub |> Hub.subscribe (QueueStrategy.BackPressure 1)
          let! accepted = tryHub |> Hub.tryPublish 1
          let! refused = tryHub |> Hub.tryPublish 2

          return
              [ equal "historian" [ 1..6 ] history
                equal "display" [ 6 ] latest
                equal "alarms" [ 1 ] firstAlarm
                equal "tryPublish accepts, then refuses when full" (true, false) (accepted.IsSome, refused.IsSome) ]
      })

      test "SubscriptionRef.changes starts with the current value then follows every update" (flow {
          let! reading = SubscriptionRef.make 0
          do! reading |> SubscriptionRef.set 1
          let! shown = Deferred.make<Axial.ClockEnvironment, Never, unit> ()

          let! view =
              reading
              |> SubscriptionRef.changes QueueStrategy.Unbounded
              |> FlowStream.tapFlow (fun _ -> Deferred.succeed () shown |> Flow.ignore)
              |> FlowStream.take 3
              |> FlowStream.runCollect
              |> Flow.fork

          do! Deferred.await shown
          do! reading |> SubscriptionRef.set 2
          do! reading |> SubscriptionRef.update ((+) 1)
          let! values = Fiber.join view
          return [ equal "values" [ 1; 2; 3 ] values ]
      })

      test "Stream operators transform, batch, and stop early" (flow {
          let! values =
              FlowStream.fromSeq [ 1..10 ]
              |> FlowStream.map (fun value -> value * 10)
              |> FlowStream.filter (fun value -> value % 20 = 0)
              |> FlowStream.chunkBySize 2
              |> FlowStream.take 2
              |> FlowStream.runCollect

          return [ equal "values" [ [ 20; 40 ]; [ 60; 80 ] ] values ]
      })

      test "mergePar, buffer, and runIntoQueue keep every value and each source's order" (flow {
          let source reader = FlowStream.fromSeq [ for sample in 1..50 -> reader, sample ]
          let! (inputs: Queue<int * int>) = Queue.bounded 4
          let! consumer = inputs |> FlowStream.fromDequeue |> FlowStream.runCollect |> Flow.fork

          do!
              [ source 1; source 2; source 3 ]
              |> FlowStream.mergePar
              |> FlowStream.buffer (QueueStrategy.BackPressure 3)
              |> FlowStream.runIntoQueue inputs

          do! Dequeue.shutdown inputs
          let! received = Fiber.join consumer
          let ordered reader = received |> List.filter (fst >> (=) reader) |> List.map snd

          return
              [ equal "count" 150 received.Length
                isTrue "each source kept its order" ([ 1..3 ] |> List.forall (fun reader -> ordered reader = [ 1..50 ])) ]
      })

      test "A failing source fails the merged stream after its buffered values" (flow {
          let! exit =
              [ FlowStream.fromSeq [ 1; 2 ]; FlowStream.fromFlow (Flow.fail "boom") ]
              |> FlowStream.mergePar
              |> FlowStream.runCollect
              |> exitOf

          return [ equal "outcome" (Exit.Failure(Cause.Fail "boom")) exit ]
      })

      test "groupedWithin emits on size and on time" (flow {
          let gapped =
              FlowStream.fromSeq [ 1..5 ]
              |> FlowStream.tapFlow (fun value -> if value = 4 then Flow.sleep (TimeSpan.FromMilliseconds 150.0) else Flow.ok ())

          let! groups = gapped |> FlowStream.groupedWithin 3 (TimeSpan.FromMilliseconds 50.0) |> FlowStream.runCollect
          return [ equal "groups" [ [ 1; 2; 3 ]; [ 4; 5 ] ] groups ]
      })

      test "The torture pipeline keeps every sample, in order, under interruption and graceful shutdown" (flow {
          let recorded = ResizeArray<int * int>()
          let stolen = ResizeArray<int * int>()

          do!
              flow {
                  let! (inputs: Queue<int * int>) = Queue.bounded 4
                  let! (feed: Hub<int * int>) = Hub.make ()
                  let! historian = feed |> Hub.subscribe (QueueStrategy.BackPressure 8)

                  let! _ =
                      historian
                      |> FlowStream.fromDequeue
                      |> FlowStream.runForEach recorded.Add
                      |> Flow.forkGraceful (Hub.shutdown feed) (TimeSpan.FromSeconds 10.0)

                  let! _ =
                      inputs
                      |> FlowStream.fromDequeue
                      |> FlowStream.runForEachFlow (fun sample -> feed |> Hub.publish sample |> Flow.ignore)
                      |> Flow.forkGraceful (Dequeue.shutdown inputs) (TimeSpan.FromSeconds 10.0)

                  let readers =
                      [ for reader in 1..3 -> FlowStream.fromSeq [ for sample in 1..100 -> reader, sample ] ]
                      |> FlowStream.mergePar
                      |> FlowStream.runIntoQueue inputs

                  let saboteur =
                      flow {
                          for _ in 1..100 do
                              let! taker = Dequeue.take inputs |> Flow.fork
                              let! exit = Fiber.interrupt taker

                              match exit with
                              | Exit.Success sample -> stolen.Add sample
                              | Exit.Failure _ -> ()
                      }

                  do! Flow.zipPar readers saboteur |> Flow.ignore
              }
              |> Flow.scoped

          let history = List.ofSeq recorded
          let everySample = [ for reader in 1..3 do for sample in 1..100 -> reader, sample ]

          let inOrder reader =
              history |> List.filter (fst >> (=) reader) |> List.pairwise |> List.forall (fun (a, b) -> snd a < snd b)

          return
              [ equal "every sample recorded or won, exactly once" everySample (List.sort (history @ List.ofSeq stolen))
                isTrue "each reader's samples in order" ([ 1..3 ] |> List.forall inOrder) ]
      }) ]
