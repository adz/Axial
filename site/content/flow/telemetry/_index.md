---
title: Axial.Flow.Telemetry
linkTitle: Telemetry
weight: 110
type: docs
---

This page shows how Flow annotations and trace metadata enter .NET activities and telemetry spans.
For the big picture — how traces, logs, and metrics fit together and how to wire up OpenTelemetry — see
[Observability]({{< relref "/flow/observability.md" >}}). On Fable JavaScript targets, the counterpart
package is `Axial.Flow.Telemetry.JavaScript` (`Otel.trace`, same tag vocabulary, emitting through
OpenTelemetry JS); its wiring is shown on the Observability page.

- [Execution and outcomes](../core-concepts/execution-and-outcomes/)
- [Explicit services and runtimes](../services-and-runtimes/)

## Workflow spans

`Activity.trace` wraps a flow in a span on the `Axial.Flow` activity source. The span covers the workflow's
execution — it stops when the workflow settles, so asynchronous work is measured — and the final exit is
stamped onto it:

| Exit | Status | Tags |
| --- | --- | --- |
| `Success` | `Ok` | `axial.flow.outcome = success` |
| `Cause.Fail` | `Error` | `axial.flow.outcome = fail`, `axial.flow.error` (rendered typed error) |
| `Cause.Die` | `Error` | `axial.flow.outcome = die`, OTel `exception.*` tags |
| `Cause.Interrupt` | unset | `axial.flow.outcome = interrupt`, `axial.flow.interrupted = true` |
| composite | dominant branch | plus `axial.flow.cause` with the pretty-printed cause tree |

Spans also carry `axial.flow.fiber.id` (see `Flow.Runtime.fiberId`), the environment identity traits
(`axial.flow.request_id`, `axial.flow.correlation_id`, `axial.flow.tenant_id`), any tags from an
`IHasTelemetryTags` environment, and every runtime annotation as `axial.flow.annotation.*` — including
annotations set in nested regions, since `Activity.trace` composes annotation sinks rather than replacing
them. Typed errors are rendered with `string`; use `Activity.traceWith` to supply a custom renderer.
`Flow.tracedError` adds `Cause.Traced` nodes that show up in the `axial.flow.cause` tree.

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

## Span-per-fiber

`FiberTelemetry.observeWithSpans` upgrades every forked fiber to a real `axial.flow.fiber` span: opened at
the fork site (so it parents to the workflow span that forked it), closed when the fiber settles, and stamped
with the fiber's status and outcome using the same conventions as workflow spans. Unobservable defects still
produce an `axial.flow.fiber.unobserved_defect` span, linked back to the fiber span. Span-per-fiber is opt-in;
hot paths forking many fibers can stay on the defect-only `FiberTelemetry.observe`.

## Logging

`Axial.Flow.Hosting` ships the `Microsoft.Extensions.Logging` counterpart: `FiberLogging.observe logger`
writes fiber defects as errors and unobserved defects as critical entries, with the exception attached so
stack traces survive. Stack it with telemetry from one edge install:

```fsharp
open Axial.Flow.Hosting

application
|> Flow.withFiberObserver
    (FiberObserver.compose FiberTelemetry.observerWithSpans (FiberLogging.observer logger))
```

The explicit `ILog` service also carries exceptions now: `Log.errorExn`/`Log.criticalExn` (and the general
`Log.logException`) preserve the exception through the Hosting bridge to the host logger.
