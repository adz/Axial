---
title: Torture test
description: Run queues, hubs, SubscriptionRef, and graceful fibers together under interruption and shutdown, and check every guarantee at once.
---

# Torture test

The [queue](queue.html), [hub](hub.html), [SubscriptionRef](subscription-ref.html), and [fiber](fibers.html) pages each
state a guarantee about a race: an interrupted take never loses a value, a lossless subscriber sees every value in
order, a late joiner sees no gap, a consumer drains its backlog when its scope closes. Each guarantee is easy to state
and easy to break by accident, and a race that breaks it only rarely does not show up in a small example.

This page runs all of them at once in one small monitoring pipeline, then checks every invariant. Because the program
reports the invariants it found violated, and the interleaving differs on every run, the only correct answer is an empty
list. The same program runs 20 times in every build of Axial's test suite, in `tests/Axial.Tests/TortureTests.fs`.

## The pipeline

- Four device readers each produce 500 numbered samples. `FlowStream.mergePar` runs them concurrently, and
  `FlowStream.runIntoQueue` feeds them into a bounded input queue of 8, so the readers are held back whenever the
  control loop falls behind.
- A control loop takes every input, publishes it to a hub, and stores it in a `SubscriptionRef` holding the latest
  sample.
- The hub feeds three subscribers with different strategies: a lossless historian (`BackPressure 32`), a display that
  only wants the newest sample (`Sliding 1`), and an alarm panel that keeps the first samples it has not handled
  (`Dropping 4`). The display and the alarm panel never take anything, so their buffers stay full the whole time.
- A late view follows the latest-sample reference for 200 changes.
- While the readers run, a saboteur keeps starting takes on the input queue and interrupting them at once, 300 times.
  Some interruptions land at the exact moment a sample is handed to the take.
- The whole pipeline lives in one `Flow.scoped` block. The historian, the view, and the control loop are forked with
  `Flow.forkGraceful`, so closing the scope first stops the control loop by shutting its queue down, then stops the
  historian by shutting the hub down, and each drains its backlog before it ends.

## The program

```fsharp transcript
> (flow {
-     let readers = 4
-     let samplesPerReader = 500
-     let everySample = [ for reader in 1..readers do for sample in 1..samplesPerReader -> reader, sample ]
-     let recorded = ResizeArray<int * int>()
-     let stolen = ResizeArray<int * int>()
-     let published = ResizeArray<int * int>()
-     let lateView = ResizeArray<int * int>()
-     let! latest = Axial.State.SubscriptionRef.make (0, 0)
-     let! inputs, display, alarms =
-         flow {
-             let! (inputs: Queue<int * int>) = Queue.bounded 8
-             let! (feed: Hub<int * int>) = Hub.make ()
-             let! historian = feed |> Hub.subscribe (QueueStrategy.BackPressure 32)
-             let! display = feed |> Hub.subscribe (QueueStrategy.Sliding 1)
-             let! alarms = feed |> Hub.subscribe (QueueStrategy.Dropping 4)
-             // The historian must record everything, including what is still queued when the scope closes.
-             let! _ =
-                 historian
-                 |> FlowStream.fromDequeue
-                 |> FlowStream.runForEach recorded.Add
-                 |> Flow.forkGraceful (Hub.shutdown feed) (System.TimeSpan.FromSeconds 30.0)
-             // A view that joins the latest-sample reference and follows 200 changes of it.
-             let! _ =
-                 latest
-                 |> Axial.State.SubscriptionRef.changes QueueStrategy.Unbounded
-                 |> FlowStream.take 200
-                 |> FlowStream.runForEach lateView.Add
-                 |> Flow.forkGraceful (Flow.ok ()) (System.TimeSpan.FromSeconds 30.0)
-             // The control loop publishes every input and keeps the latest sample current.
-             let! _ =
-                 inputs
-                 |> FlowStream.fromDequeue
-                 |> FlowStream.runForEachFlow (fun sample ->
-                     flow {
-                         do! feed |> Hub.publish sample |> Flow.ignore
-                         published.Add sample
-                         do! latest |> Axial.State.SubscriptionRef.set sample
-                     })
-                 |> Flow.forkGraceful (Dequeue.shutdown inputs) (System.TimeSpan.FromSeconds 30.0)
-             // Device readers feed the control loop concurrently, while a saboteur keeps starting takes on the
-             // same queue and interrupting them at once. A take that wins a sample before its interruption keeps
-             // it; every other sample it was handed must go back to the queue.
-             let readersFeed =
-                 [ for reader in 1..readers -> FlowStream.fromSeq [ for sample in 1..samplesPerReader -> reader, sample ] ]
-                 |> FlowStream.mergePar
-                 |> FlowStream.runIntoQueue inputs
-             let saboteur =
-                 flow {
-                     for _ in 1..300 do
-                         let! taker = inputs |> Dequeue.take |> Flow.fork
-                         let! exit = Fiber.interrupt taker
-                         match exit with
-                         | Exit.Success sample -> stolen.Add sample
-                         | Exit.Failure _ -> ()
-                 }
-             do! Flow.zipPar readersFeed saboteur |> Flow.ignore
-             return inputs, display, alarms
-         }
-         |> Flow.scoped
-     // The scope has closed: the control loop drained its queue, then the historian drained the hub.
-     let! inputStats = Dequeue.stats inputs
-     let! displayStats = Dequeue.stats display
-     let! shown = Dequeue.takeAll display
-     let! alarmStats = Dequeue.stats alarms
-     let! raised = Dequeue.takeAll alarms
-     let violations = ResizeArray<string>()
-     let check name holds = if not holds then violations.Add name
-     let history = List.ofSeq recorded
-     let order = List.ofSeq published
-     let view = List.ofSeq lateView
-     check "every sample is recorded or was won by a take, exactly once" (List.sort (history @ List.ofSeq stolen) = everySample)
-     check "the historian recorded every published sample, in publish order" (history = order)
-     check
-         "each reader's samples stay in order"
-         ([ 1..readers ] |> List.forall (fun reader -> history |> List.filter (fst >> (=) reader) |> List.pairwise |> List.forall (fun (a, b) -> snd a < snd b)))
-     check "the input queue is shut down and empty" (inputStats.IsShutdown && inputStats.Size = 0)
-     check "the sliding display ends on the newest sample" (shown = [ List.last history ])
-     check
-         "the sliding display accepted every sample and evicted all but one"
-         (displayStats.Accepted = int64 history.Length && displayStats.Evicted = int64 history.Length - 1L)
-     check "the dropping alarms kept the first four samples" (raised = List.truncate 4 history)
-     check "the dropping alarms counted every other sample as dropped" (alarmStats.Dropped = int64 history.Length - 4L)
-     // The view starts with whatever was current when it joined, then follows every later update.
-     let start = List.tryFindIndex ((=) (List.head view)) order
-     let expected =
-         match start with
-         | Some index -> order |> List.skip index |> List.truncate view.Length
-         | None -> (0, 0) :: (order |> List.truncate (view.Length - 1))
-     check "the late view saw the current sample, then every update without a gap or duplicate" (view = expected)
-     return List.ofSeq violations
- } : Flow<unit, Never, string list>)
- |> Flow.run ();;
val it: Exit<string list,Never> = Success []
```

## What each check proves

| Check | Guarantee | What would go wrong without it |
| --- | --- | --- |
| Every sample is recorded or was won by a take, exactly once | An interrupted take never loses a value: a sample handed to a take in the same instant as its interruption goes back to the front of the queue. | Samples handed to an interrupted take would vanish, and the count would come up short by a different amount on each run. |
| The historian recorded every published sample, in publish order | A `BackPressure` subscription is lossless, and `Flow.forkGraceful` lets the historian drain the hub after shutdown before it is interrupted. | Closing the scope would interrupt the historian with its backlog still buffered, and the last samples would never be written. |
| Each reader's samples stay in order | `mergePar`, the input queue, and the hub keep each producer's order, even while the saboteur's takes are interrupted. The queue serves waiting takers one at a time, so a sample given back by an interrupted take returns ahead of everything later. | If the control loop could take a later sample while an earlier one was in the hands of an interrupted take, the earlier one would come back after it, and the historian would record a sensor's readings out of order. |
| The input queue is shut down and empty | The control loop's stop request shut the queue down, and the loop drained it before it ended. | A plain `Flow.fork` would leave samples in the queue when the scope closed. |
| The sliding display ends on the newest sample | A `Sliding` subscription always keeps the latest value and never holds up the publisher. | A display would show stale data, or its full buffer would stall the whole feed. |
| The sliding display accepted every sample and evicted all but one | `Dequeue.stats` counts every eviction, so losses are visible to monitoring through `QueueMetrics`. | A lossy consumer would drop data with nothing to alert on. |
| The dropping alarms kept the first four samples | A `Dropping` subscription keeps the oldest values and discards newer ones while full, in the hub's order. | The alarm panel would lose the first alarms instead of the later ones. |
| The dropping alarms counted every other sample as dropped | Drops are counted per subscription. | Same as the eviction check. |
| The late view saw the current sample, then every update without a gap or duplicate | `SubscriptionRef.changes` reads the current value and subscribes while no update is in progress. | A view that read the value and then subscribed could miss the update made between the two, or see one twice. |

Every structure in the program is created inside the scope, and every fiber it forks owns a child scope, so nothing
outlives the pipeline: the subscriptions end with the scope, and the late view's subscription ends when its stream does.
