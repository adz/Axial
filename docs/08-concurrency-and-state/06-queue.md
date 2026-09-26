---
title: Queue
description: Hand values between fibers with back-pressure, overflow strategies, and a draining shutdown.
---

# Queue

`Queue<'a>` passes values from producer fibers to a consumer. It is FIFO, suspends instead of blocking a thread, and
keeps Axial's interruption guarantees: interrupting a suspended `Queue.take` never loses an element, and interrupting
a suspended `Queue.offer` never enqueues its value.

Use a queue when there is exactly one logical consumer. Each value goes to exactly one taker, so several fibers taking
from one queue share its values between them rather than each seeing all of them.

## Choosing a strategy

The constructor decides what `Queue.offer` does when the queue is full.

| Constructor | Full queue | `offer` returns |
| --- | --- | --- |
| `Queue.bounded n` | suspends the producer until a value is taken | `true` |
| `Queue.dropping n` | discards the new value | `false` |
| `Queue.sliding n` | evicts the oldest value | `true` |
| `Queue.unbounded ()` | never full; memory grows with the backlog | `true` |

`bounded` is the lossless choice: a slow consumer slows its producers down. `sliding` suits a latest-value feed such as
a display, where old readings are worth less than new ones.

```fsharp transcript
> (flow {
-     let! (display: Queue<int>) = Queue.sliding 3
-     do! display |> Queue.offerAll [ 1..6 ] |> Flow.ignore
-     return! Queue.takeAll display
- } : Flow<unit, Never, int list>)
- |> Flow.run ();;
val it: Exit<int list,Never> = Success [4; 5; 6]
```

## Producing and consuming

`Queue.take` suspends while the queue is empty. `Queue.poll`, `Queue.takeUpTo`, and `Queue.takeAll` never suspend, so a
consumer that writes in batches can take whatever has accumulated without one suspension per element.

`FlowStream.fromQueue` turns a queue into a stream for its consumer. The stream suspends while the queue is empty and
ends normally once the queue is shut down and drained.

```fsharp transcript
> (flow {
-     let! (readings: Queue<int>) = Queue.bounded 2
-     let! consumer =
-         readings
-         |> FlowStream.fromQueue
-         |> FlowStream.chunkBySize 2
-         |> FlowStream.runCollect
-         |> Flow.fork
-     do! readings |> Queue.offerAll [ 1..5 ] |> Flow.ignore
-     do! Queue.shutdown readings
-     return! Flow.join consumer
- } : Flow<unit, Never, int list list>)
- |> Flow.run ();;
val it: Exit<int list list,Never> = Success [[1; 2]; [3; 4]; [5]]
```

The producer offered five values into a queue that holds two, so it suspended until the consumer made room.

## Shutdown

`Queue.shutdown` ends a queue without discarding its backlog:

- fibers suspended in `take` or `offer` are interrupted;
- later offers are interrupted;
- later takes return the remaining values first, then are interrupted;
- `poll`, `takeUpTo`, and `takeAll` return nothing once the queue is empty;
- calling `shutdown` again has no effect.

Draining after shutdown is what lets a consumer finish its work when an application stops. `Queue.isShutdown` reports the
state, and `Queue.awaitShutdown` suspends until it happens.

## Tying a queue to a scope

`Queue.boundedScoped` creates a bounded queue that is shut down when the current scope closes. Use it inside
`Flow.scoped` or an application root, so the queue's lifetime follows the work that owns it.

```fsharp transcript
> (flow {
-     let! (jobs: Queue<string>) =
-         flow {
-             let! (jobs: Queue<string>) = Queue.boundedScoped 8
-             do! jobs |> Queue.offerAll [ "a"; "b" ] |> Flow.ignore
-             return jobs
-         }
-         |> Flow.scoped
-     let! backlog = jobs |> FlowStream.fromQueue |> FlowStream.runCollect
-     let! closed = Queue.isShutdown jobs
-     return backlog, closed
- } : Flow<unit, Never, string list * bool>)
- |> Flow.run ();;
val it: Exit<(string list * bool),Never> = Success (["a"; "b"], true)
```

Closing the scope shut the queue, and the consumer still received the values that were already queued.

## Fairness and interruption

Suspended takers are served in the order they began waiting, and so are suspended offerers of a bounded queue.

A taker interrupted at the same moment a value is handed to it passes that value on to the next taker, or back to the
front of the queue. An offer interrupted before its value was accepted is withdrawn. An offer accepted in the same
moment as its interruption has already taken effect and reports `true`.
