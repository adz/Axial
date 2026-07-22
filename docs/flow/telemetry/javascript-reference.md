---
weight: 91
title: Reference
description: Public types and modules in Axial.Flow.Telemetry.JavaScript.
url: /flow/telemetry/javascript/reference/
build:
  list: never
---

# Axial.Flow.Telemetry.JavaScript Reference

`Axial.Flow.Telemetry.JavaScript` emits spans through a host-supplied `@opentelemetry/api` object. It does not import
or configure the JavaScript SDK.

| Surface | Purpose |
| --- | --- |
| `Context`, `Span`, `Tracer`, `TraceApi`, `ContextApi`, `OpenTelemetryApi` | Structural bindings for the API object supplied by the host |
| `Otel.install` and `Otel.installWith` | Install the tracer used by subsequent Flow telemetry |
| `Otel.uninstall` | Remove the installed tracer |
| `Otel.trace` and `Otel.traceWith` | Wrap a workflow in an OpenTelemetry span |
| `FiberTelemetry.observer` and `FiberTelemetry.observe` | Report fiber defects through the installed tracer |
| `FiberTelemetry.observerWithSpans` and `FiberTelemetry.observeWithSpans` | Create one span for each forked fiber |

See [JavaScript Telemetry]({{< relref "/flow/telemetry/javascript/" >}}) for SDK setup, context propagation, and usage.
