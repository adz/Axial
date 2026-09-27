---
title: Pipeline
description: "Queues, a hub, SubscriptionRef, and graceful fibers run together in one monitoring pipeline, stopped by closing its scope."
---

# Pipeline

The [queue](../queue.html), [hub](../hub.html), [SubscriptionRef](../subscription-ref.html), and
[fiber](../fibers.html) pages each state a guarantee about a race. This scenario runs all of them at once in one small
monitoring pipeline, with a saboteur interrupting takes the whole time, then shuts the pipeline down by closing its
scope and checks every guarantee.

Run it with `dotnet run --project examples/Axial.TortureTest -- pipeline 100`.

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

{{< snippet id="torture-pipeline" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

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
