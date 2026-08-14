---
title: JavaScript Reference
description: Public types and modules in Axial.Telemetry.JavaScript.
build:
  list: never
---

# Axial.Telemetry.JavaScript Reference

`Axial.Telemetry.JavaScript` emits spans through a host-supplied `@opentelemetry/api` object. Use `installNamed` with
an application instrumentation scope for user workflows; `install` uses the default `Axial` scope. The package does not
import or configure the JavaScript SDK.

| Surface | Purpose |
| --- | --- |
| `OtelContext`, `Span`, `Tracer`, `TraceApi`, `ContextApi`, `OpenTelemetryApi` | Structural bindings for the API object supplied by the host |
| `Otel.installNamed`, `Otel.install`, and `Otel.installWith` | Install the tracer used by subsequent Flow telemetry |
| `Otel.uninstall` | Remove the installed tracer |
| `Otel.trace` and `Otel.traceWith` | Wrap a workflow in an OpenTelemetry span |
| `FiberTelemetry.observer` and `FiberTelemetry.observe` | Report fiber defects through the installed tracer |
| `FiberTelemetry.observerWithSpans` and `FiberTelemetry.observeWithSpans` | Create one span for each forked fiber |

Ambient attributes use `Axial.Telemetry.Context` from core on both platforms; `OtelContext` is only the opaque context object owned by `@opentelemetry/api`.

See [JavaScript Telemetry](/observability/telemetry/javascript.html) for SDK setup, context propagation, and usage.
