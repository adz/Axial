---
title: "Fiber"
weight: 20
type: docs
---

This page shows the `Fiber<'error, 'value>` handle used by FsFlow concurrency. A fiber represents a flow that has already been started in the background; it keeps the workflow's typed error and success values attached to the running work. The operations that create and consume fibers are still part of the [`Flow`](../flow/) API: use [`Flow.fork`](../flow/m-flow-fork/), [`Flow.join`](../flow/m-flow-join/), and [`Flow.interrupt`](../flow/m-flow-interrupt/) when a workflow needs explicit child execution. Prefer higher-level helpers such as `Flow.zipPar` or `Flow.race` when the code only needs parallel composition.

## Core type

- [`Fiber`](./t-fiber.md):
 Represents a handle to a workflow that has already been started.
