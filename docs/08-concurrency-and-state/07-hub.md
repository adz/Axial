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
-     let! historian = hub |> Hub.subscribe (SubscriberStrategy.BackPressure 2)
-     let! display = hub |> Hub.subscribe (SubscriberStrategy.Sliding 1)
-     let! alarms = hub |> Hub.subscribe (SubscriberStrategy.Dropping 1)
-     let! recording = historian |> FlowStream.fromSubscription |> FlowStream.runCollect |> Flow.fork
-     do! hub |> Hub.publishAll [ 1..5 ] |> Flow.ignore
-     do! Hub.shutdown hub
-     let! history = Flow.join recording
-     let! latest = Subscription.takeAll display
-     let! pending = Subscription.takeAll alarms
-     return [ history; latest; pending ]
- } : Flow<unit, Never, int list list>)
- |> Flow.run ();;
val it: Exit<int list list,Never> = Success [[1; 2; 3; 4; 5]; [5]; [1]]
```

The three lists are what the historian, the display, and the alarm panel received. The display and alarm subscribers
never took anything, yet the publisher was never held up by them. The historian's buffer holds two readings, so the
publisher waited whenever the historian fell two readings behind.

Lossless delivery needs either back-pressure or unbounded memory. A `BackPressure` subscriber that stops taking values
eventually stops the publisher, and an `Unbounded` one grows without limit instead. Size a lossless subscriber's buffer
for the bursts you expect, and use `Subscription.size` to raise an alarm before it fills.

## Subscription strategies

| Strategy | Full buffer | Effect on the publisher |
| --- | --- | --- |
| `SubscriberStrategy.BackPressure n` | the publisher waits for room | can suspend `Hub.publish` |
| `SubscriberStrategy.Sliding n` | the oldest value is evicted | never |
| `SubscriberStrategy.Dropping n` | the new value is discarded | never |
| `SubscriberStrategy.Unbounded` | never full | never |

`Hub.publish` returns a `PublishResult` with the number of subscriptions that accepted the value and the number of
`Dropping` subscriptions that discarded it, so the publisher can count drops without inspecting its subscribers.

Use a hub when there can be zero or many consumers. Use a [queue](queue.html) when there is exactly one logical consumer.

## Subscriptions belong to a scope

`Hub.subscribe` registers the subscription with the current scope. Closing that scope removes the subscription and
releases a publisher waiting on it, so run `subscribe` inside `Flow.scoped` or an application root.

```fsharp transcript
> (flow {
-     let! (hub: Hub<string>) = Hub.make ()
-     let! received =
-         flow {
-             let! events = hub |> Hub.subscribe SubscriberStrategy.Unbounded
-             do! hub |> Hub.publish "while subscribed" |> Flow.ignore
-             return! Subscription.takeAll events
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

`Hub.shutdown` shuts every subscription down with the same semantics as `Queue.shutdown`. Subscribers can still take
their backlog, `FlowStream.fromSubscription` streams end normally once drained, and later publishes are interrupted.
