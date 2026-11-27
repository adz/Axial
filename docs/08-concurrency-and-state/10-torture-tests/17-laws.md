---
title: Laws
description: "Random compositions of fork, race, zipPar, scopes, timeouts, and interruption, each checked against the laws every composition must keep."
---

# Laws

The other scenarios each drive one construct in a fixed shape. This one generates the shapes. Each round builds forty
random trees from leaves that succeed, fail, die, or never finish, combined with every structured-concurrency
operation, runs them, and checks the laws that must hold whatever the tree and the interleaving.

Run it with `dotnet run --project examples/Axial.TortureTest -- laws 100`.

## What it does

- A tree is up to five levels of `Flow.fork` (then `Fiber.join`, `Fiber.interrupt`, or left to its scope), `race`,
  `zipPar`, a sequence, a scope that acquires a resource, `recover`, `fold`, `timeoutOption`, and a forked flow
  interrupted after a short delay. A leaf sleeps up to 2 ms, then succeeds, fails, dies, or hangs. A hanging leaf is
  only generated where a timeout, an interruption, or a scope's close will end it, so every tree finishes.
- Every leaf counts itself live in each node above it while it runs. Every node checks, as it ends by any outcome,
  that none of its leaves is still running.
- Each defect is a distinct exception. The runner counts where each one is seen: in the tree's outcome, in an outcome
  a fork or interrupt received, or in an unobserved-defect report from the fiber observer.
- A law that breaks prints the first tree that broke it, as an expression, so it can be rebuilt by hand.

{{< snippet id="torture-laws" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| A flow that starts work waits for it | A `zipPar`, a scoped fork, or a timeout returns only once every leaf under it has ended, by any outcome. |
| A race's loser settles before the race returns | The losing side is interrupted and has finished, including its cleanup, when `race` returns. |
| Interrupting a fiber waits for it to end | When `Fiber.interrupt` returns, nothing the fiber started is still running. |
| Cleanup ran exactly once, after its acquire | A scoped resource is released once, after it was acquired, whether its scope ended by success, failure, or interruption. |
| Every defect was reported exactly once | No defect is lost and none is reported twice, across race and timeout discards, joins, interrupts, and scope closes. |
| Interruption is never swallowed by recover or fold | `recover` and `fold` handle typed failures only: a source that was interrupted never becomes a success. |

## What it found

Two runtime bugs, fixed in 0.17.0, each pinned by a test:

- A timeout around a scoped fork could not stop a flow waiting in `Fiber.join`, so a child that never ended hung the
  timeout forever. Interrupting a join now stops the wait, not the fiber.
- An unjoined fiber that died with several defects reported only the first as unobserved.
