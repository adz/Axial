---
title: Queue
description: Hand values between fibers with back-pressure, overflow strategies, batching, and a draining shutdown.
---

# Queue

`Queue<'a>` passes values from producer fibers to a consumer. It is FIFO, suspends instead of blocking a thread, and
keeps Axial's interruption guarantees: interrupting a suspended take never loses an element, and interrupting a
suspended `Queue.offer` never enqueues its value.

Use a queue when there is exactly one logical consumer. Each value goes to exactly one taker, so several fibers taking
from one queue share its values between them. When zero or many consumers must each see every value, use a
[hub](hub.html).

A queue has two sides. The `Queue` module creates queues and offers to them. The `Dequeue` module takes from them,
inspects them, and shuts them down; its functions accept a `Queue`, and a hub subscription is a `Dequeue` too, so a
consumer is written once for both.

## Choosing a strategy

A `QueueStrategy` decides what `Queue.offer` does when the queue is full.

| Strategy | Shorthand | Full queue | `offer` returns |
| --- | --- | --- | --- |
| `QueueStrategy.BackPressure n` | `Queue.bounded n` | suspends the producer until a value is taken | `true` |
| `QueueStrategy.Dropping n` | `Queue.dropping n` | discards the new value | `false` |
| `QueueStrategy.Sliding n` | `Queue.sliding n` | evicts the oldest value | `true` |
| `QueueStrategy.Unbounded` | `Queue.unbounded ()` | never full; memory grows with the backlog | `true` |

`BackPressure` is the lossless choice: a slow consumer slows its producers down. `Sliding` suits a latest-value feed
such as a display, where old readings are worth less than new ones.

```fsharp transcript
> (flow {
-     let! (display: Queue<int>) = Queue.make (QueueStrategy.Sliding 3)
-     do! display |> Queue.offerAll [ 1..6 ] |> Flow.ignore
-     return! Dequeue.takeAll display
- } : Flow<unit, Never, int list>)
- |> Flow.run ();;
val it: Exit<int list,Never> = Success [4; 5; 6]
```

## Producing and consuming

`Dequeue.take` suspends while the queue is empty. `Dequeue.poll`, `Dequeue.takeUpTo`, and `Dequeue.takeAll` never
suspend.

`FlowStream.fromDequeue` turns a queue into a stream for its consumer. The stream suspends while the queue is empty and
ends normally once the queue is shut down and drained.

```fsharp transcript
> (flow {
-     let! (readings: Queue<int>) = Queue.bounded 2
-     let! consumer =
-         readings
-         |> FlowStream.fromDequeue
-         |> FlowStream.chunkBySize 2
-         |> FlowStream.runCollect
-         |> Flow.fork
-     do! readings |> Queue.offerAll [ 1..5 ] |> Flow.ignore
-     do! Dequeue.shutdown readings
-     return! Fiber.join consumer
- } : Flow<unit, Never, int list list>)
- |> Flow.run ();;
val it: Exit<int list list,Never> = Success [[1; 2]; [3; 4]; [5]]
```

The producer offered five values into a queue that holds two, so it suspended until the consumer made room.

## Taking in batches

A consumer that writes to a database wants to wait for work, then take everything that has piled up, with one write
per batch rather than per value. `Dequeue.takeBetween min max` suspends until at least `min` values are available,
then takes up to `max`.

```fsharp transcript
> (flow {
-     let! (samples: Queue<int>) = Queue.unbounded ()
-     do! samples |> Queue.offerAll [ 1..7 ] |> Flow.ignore
-     let! first = samples |> Dequeue.takeBetween 1 5
-     let! second = samples |> Dequeue.takeBetween 1 5
-     return [ first; second ]
- } : Flow<unit, Never, int list list>)
- |> Flow.run ();;
val it: Exit<int list list,Never> = Success [[1; 2; 3; 4; 5]; [6; 7]]
```

If `takeBetween` is interrupted while it waits for the rest of its minimum, it puts the values it had collected back at
the front of the queue, in order. After shutdown it returns whatever remains, even if that is fewer than `min`.

## Shutdown

`Dequeue.shutdown` ends a queue without discarding its backlog:

- fibers suspended in a take or an offer are interrupted;
- later offers are interrupted;
- later takes return the remaining values first, then are interrupted;
- `poll`, `takeUpTo`, and `takeAll` return nothing once the queue is empty;
- calling `shutdown` again has no effect.

Draining after shutdown is what lets a consumer finish its work when an application stops. `Dequeue.isShutdown` reports
the state, and `Dequeue.awaitShutdown` suspends until it happens.

## Tying a queue to a scope

`Queue.makeScoped` creates a queue that is shut down when the current scope closes. Use it inside `Flow.scoped`, a forked
fiber, or an application root, so the queue's lifetime follows the work that owns it.

```fsharp transcript
> (flow {
-     let! (jobs: Queue<string>) =
-         flow {
-             let! (jobs: Queue<string>) = Queue.makeScoped (QueueStrategy.BackPressure 8)
-             do! jobs |> Queue.offerAll [ "a"; "b" ] |> Flow.ignore
-             return jobs
-         }
-         |> Flow.scoped
-     return! jobs |> FlowStream.fromDequeue |> FlowStream.runCollect
- } : Flow<unit, Never, string list>)
- |> Flow.run ();;
val it: Exit<string list,Never> = Success ["a"; "b"]
```

Closing the scope shut the queue down, so the stream ended once it had delivered the values that were already queued.
Without the shutdown, the consumer would have waited for more values forever.

## Fairness and interruption

Suspended takers are served in the order they began waiting, and so are suspended offerers of a bounded queue.

A taker interrupted at the same moment a value is handed to it passes that value on to the next taker, or back to the
front of the queue. An offer interrupted before its value was accepted is withdrawn. An offer accepted in the same
moment as its interruption has already taken effect and reports `true`.
