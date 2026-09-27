---
title: Semaphores
description: "Workers compete for a semaphore's permits while a third of them are interrupted, whether waiting for a permit or holding one."
---

# Semaphores

Sixty workers compete for the three permits of a [semaphore](../deferred-semaphore.html). A random third are
interrupted, some while they wait for a permit and some while they hold one.

Run it with `dotnet run --project examples/Axial.TortureTest -- semaphores 100`.

## What it does

- Each worker takes a permit with `Semaphore.withPermit`, counts itself in, sleeps briefly, and counts itself out with
  `Flow.ensuring`, so it is counted out however it ends.
- After the workers have settled, three flows each take a permit and wait on a `Deferred` until all three hold one at
  the same time. This only completes if every permit came back.

{{< snippet id="torture-semaphores" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| No more workers than permits ever held one at once | The semaphore bounds concurrency under contention. |
| Every worker that finished had entered | A worker completes only after taking a permit. |
| Every worker that entered was counted out, even if interrupted inside | `Flow.ensuring` runs its cleanup on interruption. |
| Interrupted waiters returned their permits | A waiter interrupted as its permit is handed over passes the permit on instead of losing it. |
