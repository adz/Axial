---
title: Hubs
description: "A hub with subscribers joining and leaving throughout, every buffer strategy, and every publish style, then shut down."
---

# Hubs

One publisher sends thousands of values to a [hub](../hub.html) while short-lived subscribers join at random, read a
few values with a random buffer strategy, and leave. Two subscribers stay for the whole run.

Run it with `dotnet run --project examples/Axial.TortureTest -- hubs 100`.

## What it does

- The publisher mixes `Hub.publish`, `Hub.publishAll` batches, and `Hub.tryPublish`, adding up the `PublishResult` of
  each.
- One lifetime subscriber reads its `Hub.subscribe` subscription directly; the other reads through
  `FlowStream.fromHub`. The publisher waits until both are subscribed.
- Forty visitors each subscribe with `Unbounded`, `BackPressure 8`, `Sliding 2`, or `Dropping 2`, take up to 40 values,
  and leave by shutting their subscription down, then drain what is left.
- The hub is shut down after the last publish. A second part creates a hub with `Hub.makeScoped`, publishes with
  `publishAll` and `FlowStream.runIntoHub`, and drains the subscription after the scope has closed.

{{< snippet id="torture-hubs" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| The lifetime subscribers each saw every value, in order | A lossless subscription gets every value in publish order, whether read directly or as a stream. |
| Every lossless visitor saw an unbroken run of values | A subscriber that joins late gets every value published after it joined, with no gap. |
| Every lossy visitor saw values in publish order | `Sliding` and `Dropping` subscriptions lose values but never reorder them. |
| Publish results add up to what the subscriptions accepted, dropped, and evicted | `PublishResult` reports exactly what happened in each subscription, so monitoring can trust it. |
| Shutdown ended the streams and emptied the hub | `Hub.shutdown` ends every subscription, and the subscriber count drops to zero. |
| A scoped hub shuts down with its scope, and its subscriber drains everything | Closing the scope shuts the hub down without discarding the subscriber's backlog. |
