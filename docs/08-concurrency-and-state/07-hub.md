---
title: Hub
description: Broadcast every value to many subscribers, each with its own buffering strategy.
---

# Hub

`Hub<'a>` delivers every published value to every current subscription. Each subscription chooses how it buffers, so
one publisher can feed a consumer that must see every value alongside consumers that only want the latest one.

Here one sensor feed goes to three subscribers: a historian that must record every reading, a display that only needs
the newest reading, and an alarm panel that keeps the first reading it has not yet handled.

```fsharp transcript
> (flow {
-     let! (hub: Hub<int>) = Hub.make ()
-     let! historian = hub |> Hub.subscribe (QueueStrategy.BackPressure 2)
-     let! display = hub |> Hub.subscribe (QueueStrategy.Sliding 1)
-     let! alarms = hub |> Hub.subscribe (QueueStrategy.Dropping 1)
-     let! recording = historian |> FlowStream.fromDequeue |> FlowStream.runCollect |> Flow.fork
-     do! hub |> Hub.publishAll [ 1..5 ] |> Flow.ignore
-     do! Hub.shutdown hub
-     let! history = Flow.join recording
-     let! latest = Dequeue.takeAll display
-     let! pending = Dequeue.takeAll alarms
-     return [ history; latest; pending ]
- } : Flow<unit, Never, int list list>)
- |> Flow.run ();;
val it: Exit<int list list,Never> = Success [[1; 2; 3; 4; 5]; [5]; [1]]
```

The three lists are what the historian, the display, and the alarm panel received. The display and alarm subscribers
never took anything, yet the publisher was never held up by them. The historian's buffer holds two readings, so the
publisher waited whenever the historian fell two readings behind.

Lossless delivery needs either back-pressure or unbounded memory. A `BackPressure` subscriber that stops taking values
eventually stops the publisher, and an `Unbounded` one grows without limit instead. While the publisher waits for a full
`BackPressure` subscriber, no later value reaches any subscriber, lossy ones included: every subscriber sees the same
order, so one stalled lossless subscriber stalls the whole feed. Size a lossless subscriber's buffer for the bursts you
expect, and use `Dequeue.size` on the subscription to raise an alarm before it fills.

## Subscription strategies

A subscription takes the same `QueueStrategy` as a [queue](queue.html), and `Hub.subscribe` returns the subscription as
a `Dequeue`, so a subscriber consumes it with the `Dequeue` functions or `FlowStream.fromDequeue`.

| Strategy | Full buffer | Effect on the publisher |
| --- | --- | --- |
| `QueueStrategy.BackPressure n` | the publisher waits for room | can suspend `Hub.publish` |
| `QueueStrategy.Sliding n` | the oldest value is evicted | never |
| `QueueStrategy.Dropping n` | the new value is discarded | never |
| `QueueStrategy.Unbounded` | never full | never |

`Hub.publish` returns a `PublishResult` that counts the subscriptions that accepted the value, the `Dropping`
subscriptions that discarded it, and the `Sliding` subscriptions that evicted an older value to take it, so the
publisher can report losses without inspecting its subscribers.

Use a hub when there can be zero or many consumers. Use a [queue](queue.html) when there is exactly one logical consumer.

## Publishing without waiting

A control loop that must keep its period cannot wait for a slow historian. `Hub.tryPublish` publishes only if it can
finish without waiting, and returns `None` without delivering anything otherwise, so no subscriber sees a value that
the others missed.

```fsharp transcript
> (flow {
-     let! (hub: Hub<int>) = Hub.make ()
-     let! historian = hub |> Hub.subscribe (QueueStrategy.BackPressure 1)
-     let! first = hub |> Hub.tryPublish 1
-     let! second = hub |> Hub.tryPublish 2
-     let! recorded = Dequeue.takeAll historian
-     return first.IsSome, second.IsSome, recorded
- } : Flow<unit, Never, bool * bool * int list>)
- |> Flow.run ();;
val it: Exit<(bool * bool * int list),Never> = Success (true, false, [1])
```

Do not bound `Hub.publish` with a timeout instead. An interrupted publish leaves the value with the subscribers it has
already reached, so publishing it again delivers it to them twice.

## Subscriptions belong to a scope

`Hub.subscribe` registers the subscription with the current scope. Closing that scope removes the subscription and
releases a publisher waiting on it, so run `subscribe` inside `Flow.scoped` or an application root. A subscriber can
also leave early with `Dequeue.shutdown`, which has the same effect.

```fsharp transcript
> (flow {
-     let! (hub: Hub<string>) = Hub.make ()
-     let! received =
-         flow {
-             let! events = hub |> Hub.subscribe QueueStrategy.Unbounded
-             do! hub |> Hub.publish "while subscribed" |> Flow.ignore
-             return! Dequeue.takeAll events
-         }
-         |> Flow.scoped
-     do! hub |> Hub.publish "after the scope closed" |> Flow.ignore
-     let! count = Hub.subscriberCount hub
-     return received @ [ $"subscribers left: {count}" ]
- } : Flow<unit, Never, string list>)
- |> Flow.run ();;
val it: Exit<string list,Never> = Success ["while subscribed"; "subscribers left: 0"]
```

## Ordering and late subscribers

Publishes are serialized: every subscriber observes values in the same order, even when several fibers publish
concurrently. A subscriber sees only values published after its `subscribe` completes. A consumer that joins late and
needs the current state must get it from the application, for example a snapshot combined with sequence numbers.

## Shutdown

`Hub.shutdown` shuts every subscription down with the same semantics as `Dequeue.shutdown`. Subscribers can still take
their backlog, `FlowStream.fromDequeue` streams end normally once drained, and later publishes are interrupted.
`Hub.makeScoped` creates a hub that is shut down when the current scope closes, and `Hub.awaitShutdown` suspends until
the hub is shut down.
