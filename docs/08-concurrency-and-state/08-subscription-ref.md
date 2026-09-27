---
title: SubscriptionRef
description: Keep a current value that consumers can join at any time and then follow every change.
---

# SubscriptionRef

`SubscriptionRef<'a>` is a reference whose changes can be streamed. `SubscriptionRef.changes` starts with the value that
is current when the stream starts, then delivers every later update. Nothing falls between the two: an update made
after that first value always reaches the stream, and no update arrives twice.

Use it when consumers join at different times and each needs the current state before the updates, such as a display
that opens after a sensor feed started. A [hub](hub.html) subscriber sees only values published after it subscribes, so
it would wait for the next reading with nothing to show.

```fsharp transcript
> (flow {
-     let! (temperature: Axial.State.SubscriptionRef<float>) = Axial.State.SubscriptionRef.make 20.0
-     do! temperature |> Axial.State.SubscriptionRef.set 21.5
-     let! shown = Deferred.make<unit, Never, unit> ()
-     let! display =
-         temperature
-         |> Axial.State.SubscriptionRef.changes QueueStrategy.Unbounded
-         |> FlowStream.tapFlow (fun _ -> Deferred.succeed () shown |> Flow.ignore)
-         |> FlowStream.take 3
-         |> FlowStream.runCollect
-         |> Flow.fork
-     do! Deferred.await shown
-     do! temperature |> Axial.State.SubscriptionRef.set 22.0
-     do! temperature |> Axial.State.SubscriptionRef.update (fun value -> value + 0.5)
-     return! Fiber.join display
- } : Flow<unit, Never, float list>)
- |> Flow.run ();;
val it: Exit<float list,Never> = Success [21.5; 22.0; 22.5]
```

The display joined after the value had changed from `20.0` to `21.5`, so it started from `21.5`. The writer waited until
the display had shown that reading, then made two updates, and the display saw both.

## Choosing how each stream buffers

`changes` takes a [`QueueStrategy`](queue.html) for that stream alone. A display that only needs the newest value uses
`QueueStrategy.Sliding 1`: while it is busy, older updates are replaced and the writer never waits. A recorder that must
see every change uses `QueueStrategy.Unbounded`, or `QueueStrategy.BackPressure n` when a full buffer should make
updates wait for it.

## Reading and updating

`SubscriptionRef.get` reads the current value. `set`, `update`, and `modify` change it and deliver the new value to every
running `changes` stream before the next update starts. `modify` takes a function returning `(result, newValue)`, in the
same order as `Ref.modify`.

A `changes` stream subscribes when it starts and unsubscribes when it ends. It does not end on its own: stop it with
`FlowStream.take` or `FlowStream.takeWhile`, by interrupting its consumer, or by closing the consumer's scope.

The [torture test](torture-test.html) checks that a late view sees no gap and no duplicate while updates race with it.
