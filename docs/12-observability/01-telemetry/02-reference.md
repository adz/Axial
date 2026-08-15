---
title: Telemetry Reference
description: Public modules in Axial.Telemetry for .NET tracing, fiber spans, metrics, and dumps.
---

# Axial.Telemetry Reference

`Axial.Telemetry` connects Flow execution and fiber lifecycle to .NET `ActivitySource` and `Meter` APIs. The host
still chooses and configures its OpenTelemetry listeners, exporters, and sampling.

| Surface | Members | Purpose |
| --- | --- | --- |
| `AttributeKey` | `string`, `boolean`, `int64`, `float`, and list variants | Define a typed application attribute |
| `Context` | `attribute`, `current`, construction, scoping, inspection, and semantic helpers | Carry typed telemetry attributes through the ambient Flow runtime |
| `ActivityTracer` | `create`, `.Trace` | Capture an application-owned `ActivitySource` once and trace from it without repeating the source per call site |
| `Activity` | `runtimeSource`, `traceOn`, `traceWithSource`, `withTracer`, `trace`, `traceWith` | Wrap a workflow in an Activity and stamp its final Exit |
| `FiberTelemetry` | `observer`, `observe`, `observerWithSpans`, `observeWithSpans` | Report fiber defects or create one span per fiber |
| `FiberMetrics` | `meter`, `observer`, `observe` | Record fiber starts, live counts, settlement, duration, and unobserved defects |
| `FiberDumpTelemetry` | `record` | Add a live-fiber tree to the current trace or a standalone span |

Start with `Context.withAttributes` and `Activity.traceOn applicationActivitySource` for searchable application
workflow spans. Register that source name and `"Axial"` with OpenTelemetry. The application source owns user operation
spans; Axial's `runtimeSource` owns automatic runtime and fiber spans — trace application workflows through your own
source, not Axial's. When many call sites share one application source, capture it once with `ActivityTracer.create`
and either call `.Trace` on the tracer directly, or install it ambiently for a whole workflow tree with
`Activity.withTracer` and call the ambient `Activity.trace`/`Activity.traceWith` underneath it. Install
`FiberTelemetry.observe` once at the application edge when unjoined child defects must be visible. Use the
span-per-fiber observer only when that extra span volume is useful.

See [Telemetry](/observability/telemetry/index.html) for setup and complete examples.
