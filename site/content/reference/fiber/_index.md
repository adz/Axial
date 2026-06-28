---
title: "Fiber"
weight: 20
type: docs
---

This page shows the `Fiber<'error, 'value>` handle used by Axial concurrency. A fiber represents a flow that has already been started in the background; it keeps the workflow's typed error and success values attached to the running work, plus diagnostic metadata such as fiber id, parent id, start time, and lifecycle status. The operations that create and consume fibers are still part of the [`Flow`](../flow/) API: use [`Flow.fork`](../flow/concurrency/m-flow-fork.md), [`Flow.join`](../flow/concurrency/m-flow-join.md), and [`Flow.interrupt`](../flow/concurrency/m-flow-interrupt.md) when a workflow needs explicit child execution. Prefer higher-level helpers such as `Flow.zipPar` or `Flow.race` when the code only needs parallel composition.

## Core types

- [`Flow.Fiber`](./t-flow-fiber.md):
 Represents a handle to a workflow that has already been started.

- [`Flow.FiberId`](./t-flow-fiberid.md): Unique identifier for a running fiber.
- [`Flow.FiberStatus`](./t-flow-fiberstatus.md): Describes the current lifecycle state of a fiber.
- [`Flow.FiberMetadata`](./t-flow-fibermetadata.md): Diagnostic metadata for a running fiber.
- [`Flow.FiberDump`](./t-flow-fiberdump.md): Human-readable diagnostic dump for a fiber.

## Module functions

- [`Flow.Fiber.dump`](./m-flow-fiber-dump.md): Returns a snapshot of the current fiber metadata.
