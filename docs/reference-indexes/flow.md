---
title: Reference
type: docs
notoc: true
weight: 100
---

API reference for the Flow area, grouped by package.

## Axial.Flow

Start with [`Flow`](./flow/) — the central execution type: a cold computation that reads `env`, returns a typed
failure or success value, and preserves interruption and defects. Use [`flow { }`](./flow/builders-flow/) for normal
orchestration syntax.

- [`Flow`](./flow/) — construction, composition, environment, execution, and runtime operations.
- [`Fiber`](./fiber/) — the handle returned by `Flow.fork`; running child work that can be joined or interrupted.
- [`Exit`](./exit/) — the outcome of a completed execution: `Result<'value, Cause<'error>>`.
- [`Cause`](./cause/) — why a flow failed: typed failure, defect, or interruption.
- [`Concurrency`](./concurrency/) — parallel composition, racing, and coordination primitives.
- [`Schedule`](./schedule/) — retry and repeat policy.
- [`Ref`](./ref/) — atomic mutable references.
- [`STM`](./stm/) — transactional memory (.NET only).
- [`Stream`](./stream/) — effectful pull-based streams.
- [`Bind`](./bind/) — bind-site error assignment and mapping inside `flow {}`.
- [`Service`](./service/) — nominal service contracts and provider-edge lookup.
- [`Layer`](./layer/) — environment provisioning with owned cleanup.
- [`Scope`](./scope/) — resource lifetime and cleanup ownership.

## Service packages

- [`Core services`](./service/core/) — clock, log, random, guid, environment variables (`Axial.Flow.PlatformService`).
- [`Console`](./service/console/) — `Axial.Flow.Console`.
- [`FileSystem`](./service/filesystem/) — `Axial.Flow.FileSystem`.
- [`Http`](./service/http/) — `Axial.Flow.Http`.
- [`Process`](./service/process/) — `Axial.Flow.Process`.
