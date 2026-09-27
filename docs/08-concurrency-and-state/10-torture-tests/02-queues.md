---
title: Queues
description: "Producers and consumers on one queue with takes and offers interrupted at random, every taking style, every strategy, and shutdown mid-stream."
---

# Queues

Four producers and three consumers share one [queue](../queue.html) with a back-pressure buffer of 8. Every take and
offer can be interrupted at the moment a value changes hands, and the queue is shut down while consumers are still
waiting.

Run it with `dotnet run --project examples/Axial.TortureTest -- queues 100`.

## What it does

- Producers alternate `Queue.offer` and `Queue.offerAll` batches. The buffer is full most of the time, so offers wait.
- Consumers pick a taking style at random for each take: `Dequeue.take`, `takeBetween`, `poll`, or `takeUpTo`. They
  stop when the queue is shut down and drained.
- A saboteur forks takes and interrupts each one at once. When a value is handed over at the same instant as the
  interruption, the take either keeps it and reports success, or gives it back to the front of the queue.
- A withdrawer forks offers and interrupts each one at once. An offer interrupted before it was accepted must not
  enqueue its value.
- A second part fills a dropping queue and a sliding queue from three producers while one slow reader takes, and checks
  that the counters account for every value.

{{< snippet id="torture-queues" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| Every accepted value is taken exactly once | An interrupted take never loses a value and never duplicates one: a value handed over in the same instant as the interruption is kept or given back. |
| No withdrawn offer was ever enqueued | An offer interrupted while it waits for space is withdrawn completely. |
| Each consumer saw every producer's values in order | Waiting takers are served one batch at a time, so a value given back by an interrupted take returns ahead of later values. |
| The queue counted every accepted value | `Dequeue.stats` agrees with what the producers were told. |
| No taker or offerer is left waiting | Shutdown releases every suspended take and offer. |
| The queue is shut down and empty | `Dequeue.shutdown` and `Dequeue.awaitShutdown` end the queue, and draining with `takeAll` leaves nothing behind. |
| A dropping queue accounts for every offer | Every offer is either accepted or counted as dropped, and a dropped offer reports `false`. |
| A sliding queue accepts every offer | Evictions make room, and every value is delivered or counted as evicted. |
| A scoped queue is shut down with its scope | `Queue.makeScoped` ties the queue to the enclosing scope. |
