---
weight: 90
title: Reference
description: Public modules in Axial.Flow.Telemetry for .NET tracing, fiber spans, metrics, and dumps.
type: docs
---


`Axial.Flow.Telemetry` connects Flow execution and fiber lifecycle to .NET `ActivitySource` and `Meter` APIs. The host
still chooses and configures its OpenTelemetry listeners, exporters, and sampling.

| Module | Members | Purpose |
| --- | --- | --- |
| `Activity` | `source`, `trace`, `traceWith` | Wrap a workflow in an Activity and stamp its final Exit |
| `FiberTelemetry` | `observer`, `observe`, `observerWithSpans`, `observeWithSpans` | Report fiber defects or create one span per fiber |
| `FiberMetrics` | `meter`, `observer`, `observe` | Record fiber starts, live counts, settlement, duration, and unobserved defects |
| `FiberDumpTelemetry` | `record` | Add a live-fiber tree to the current trace or a standalone span |

Start with `Activity.trace` for workflow spans. Install `FiberTelemetry.observe` once at the application edge when
unjoined child defects must be visible. Use the span-per-fiber observer only when that extra span volume is useful.

See [Telemetry]({{< relref "/flow/telemetry/" >}}) for setup and complete examples.
