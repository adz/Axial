---
title: Axial.Flow.Telemetry
linkTitle: Telemetry
weight: 110
type: docs
---

This page shows how Flow annotations and trace metadata enter .NET activities and telemetry spans.

- [Execution and outcomes](../core-concepts/execution-and-outcomes/)
- [Explicit services and runtimes](../services-and-runtimes/)

## Fiber defect spans

`FiberTelemetry.observe` installs a [fiber observer]({{< relref "/flow/concurrency/supervision.md" >}}) that records
fiber defects on the `Axial.Flow` activity source:

```fsharp
open Axial.Flow.Telemetry

application
|> FiberTelemetry.observe
```

- Every fiber that settles with a defect produces an `axial.flow.fiber.defect` error span.
- Every defect the runtime proves unobservable — a discarded `Flow.fork` handle, or a `Flow.race`/timeout loser —
  produces an `axial.flow.fiber.unobserved_defect` error span.

Spans carry `axial.flow.fiber.id`, `axial.flow.fiber.parent_id`, `axial.flow.fiber.status`, and
OpenTelemetry-convention `exception.*` tags. Use `FiberTelemetry.observer` directly with
`Flow.withFiberObserver` if you want to combine it with your own hooks.
