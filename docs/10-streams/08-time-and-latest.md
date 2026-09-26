---
title: Time and Latest Value
---

# Time and Latest Value

Most stream operators react only to values. The operators on this page also react to time, or to a newer value
arriving, which is what progress reporting, batching, and search-as-you-type need.

Each of them moves upstream values through a one-slot queue from a producer fiber, so it can wait for "the next value
or a timer" at once. The producer runs in the consuming Flow's scope: it keeps upstream back-pressure, and it stops
when the consumer finishes, fails, or stops early with `take`.

## Batch by size or time

`FlowStream.groupedWithin size window` emits a list when it holds `size` values or when `window` has passed since the
list's first value, whichever comes first. A slow trickle is not held back waiting for a full batch:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
events
|> FlowStream.groupedWithin 100 (TimeSpan.FromSeconds 1.0)
|> FlowStream.runForEachFlow writeBatch
```

No empty list is emitted, and the partial list is emitted when upstream ends or before a failure is propagated.

## Report progress at a steady rate

`FlowStream.throttle interval` emits at most one value per interval. The first value is emitted immediately; values
arriving faster replace each other, and the latest is emitted when the interval ends:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
progress
|> FlowStream.throttle (TimeSpan.FromMilliseconds 100.0)
|> FlowStream.runForEachFlow render
```

## Wait for input to settle

`FlowStream.debounce quiet` emits a value only once `quiet` passes without a newer one, so a burst produces only its
last value. The pending value is emitted when upstream ends.

## Keep only the latest request

`FlowStream.switchMapFlow mapper` runs `mapper` for each value, and interrupts the running flow when a newer value
arrives. Results for stale input are never emitted, and the superseded request's cleanup finishes before the next
one starts:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
keystrokes
|> FlowStream.debounce (TimeSpan.FromMilliseconds 200.0)
|> FlowStream.switchMapFlow search
|> FlowStream.runForEachFlow showResults
```

When upstream ends, the running flow is allowed to finish. The first failure stops the stream.

These operators wait on real timers, so tests that use them depend on timing; give assertions generous margins.
