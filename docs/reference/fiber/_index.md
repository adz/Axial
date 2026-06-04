---
title: "Fiber"
weight: 20
---

This page shows the `Fiber<'error, 'value>` handle used by FsFlow concurrency. A fiber represents a flow that has already been started in the background; it keeps the workflow's typed error and success values attached to the running work, plus diagnostic metadata such as fiber id, parent id, start time, and lifecycle status. The operations that create and consume fibers are still part of the [`Flow`](../flow/) API: use [`Flow.fork`](../flow/concurrency/m-flow-fork.md), [`Flow.join`](../flow/concurrency/m-flow-join.md), and [`Flow.interrupt`](../flow/concurrency/m-flow-interrupt.md) when a workflow needs explicit child execution. Prefer higher-level helpers such as `Flow.zipPar` or `Flow.race` when the code only needs parallel composition.

## Core types

- [`Fiber`](./t-fiber.md):
- [`FiberId`](./t-fiberid.md): Unique identifier for a running fiber.
- [`FiberStatus`](./t-fiberstatus.md): Describes the current lifecycle state of a fiber.
- [`FiberMetadata`](./t-fibermetadata.md): Diagnostic metadata for a running fiber.
- [`FiberDump`](./t-fiberdump.md): Human-readable diagnostic dump for a fiber.

## Module functions

- [`Fiber.dump`](./m-fiber-dump.md): Returns a snapshot of the current fiber metadata.
