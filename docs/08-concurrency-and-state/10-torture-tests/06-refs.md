---
title: Refs
description: "Eight fibers update a Ref and a SubscriptionRef concurrently with every update operation, while a change stream follows every value."
---

# Refs

Eight fibers update one [Ref](../ref.html) and one [SubscriptionRef](../subscription-ref.html) concurrently, using
every update operation.

Run it with `dotnet run --project examples/Axial.TortureTest -- refs 100`.

## What it does

- Each fiber adds one to a counter hundreds of times, choosing `Ref.update`, `modify`, `getAndUpdate`, or
  `updateAndGet` at random, and records the value each operation replaced.
- The fibers then pass tokens through a second ref with `Ref.getAndSet`: each call puts a new token in and takes the
  previous one out.
- A `SubscriptionRef` is incremented the same way with `update` and `modify`, while a `changes` stream that subscribed
  before the first update collects every value.

{{< snippet id="torture-refs" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| No increment was lost | Every update is atomic. |
| Every increment that reported a value saw a different one | No two updates observed the same state, so none of them overlapped. |
| getAndSet handed every token on exactly once | A swap never loses or duplicates the value it replaces. |
| A SubscriptionRef ends at the total | Its updates are atomic too. |
| Its change stream saw every value, in order | Each update is delivered to the stream before the next update starts, with no gap and no duplicate. |
