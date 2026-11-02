---
title: Observability
description: How tracing, logging, and metrics fit together across Axial packages, and how to plug in OpenTelemetry.
project: src/Axial.Telemetry/Axial.Telemetry.fsproj
---

# Observability

| Signal | Where it comes from | Consumed by |
| --- | --- | --- |
| **Traces** (spans) | [`Axial.Telemetry`](/telemetry/index.html) emitting on the `Axial` `ActivitySource`; `Axial.Telemetry.JavaScript` on Fable targets | any `ActivityListener`, in practice the OpenTelemetry SDK; OpenTelemetry JS under Fable |
| **Logs** | the runtime log sink (`Log.info` and friends), bridged to `Microsoft.Extensions.Logging` by [`Axial.Hosting`](/hosting/dotnet.html) | your host's logging pipeline |
| **Metrics** | [`Axial.Telemetry`](/telemetry/index.html): `FiberMetrics` and `QueueMetrics` on the `Axial` `Meter` | OpenTelemetry's `.AddMeter("Axial")`, `dotnet-counters`, the Aspire dashboard |
| **Fiber dumps** | core `Axial`: `FiberRegistry` live-fiber snapshots, no telemetry dependency | `registry.DumpAt(Clock.live)` on demand; `FiberDumpTelemetry.record` to put dumps on traces |

Two general-purpose channels feed those signals and are part of core `Axial`, not the telemetry
package: **runtime annotations** (`Flow.annotate`, ambient key–value diagnostics metadata) and **fiber
observers** (`FiberObserver`, lifecycle hooks for every forked fiber; see
[Supervision and fiber observability](/concurrency-and-state/supervision.html)).

## How .NET tracing works: `ActivitySource` and `ActivityListener`

Axial publishes spans through .NET's `System.Diagnostics` tracing model:

- An **`ActivitySource`** is the producer. Axial owns one, named `"Axial"`. Instrumented code calls
  `StartActivity`, and an `Activity` is a span: name, timing, tags, status, parent.
- An **`ActivityListener`** is the consumer. Nothing is recorded until the application registers a listener
  that opts into a source by name and makes the sampling decision. With no interested listener,
  `StartActivity` returns `null` and Axial skips all tagging work, so an untraced app pays roughly a null
  check per span site.
- **`Activity.Current`** is an async-local holding the ambient span. New spans parent to it automatically,
  which is how Axial spans nest inside ASP.NET Core request spans (and under an upstream `traceparent`
  header) with no wiring.

The host decides at the edge whether anything listens and where spans go; workflows do not carry a tracing dependency.

## Setup and recipes

The [Telemetry guide](/telemetry/index.html) covers the runnable setup that this overview only maps:
span tag vocabulary and `Activity.traceOn`, OpenTelemetry wiring in ASP.NET Core and console hosts, the Aspire
dashboard walkthrough, and fiber metrics and dumps. The [JavaScript guide](/telemetry/javascript.html)
covers the Fable bootstrap and cross-stack `traceparent` propagation.

## Correlation: how the signals join up

- **Telemetry context → span attributes.** `Context.withEndUserId`, `Context.withAttribute`, and
  `Context.withAttributes` scope typed, searchable attributes around a workflow. Both the .NET and JavaScript adapters
  consume the same ambient context; no environment interfaces or runtime field discovery are involved.
- **Annotations → every observer.** `Flow.annotate "payment.attempt" attemptId` is scoped runtime metadata,
  not a tracing call: any active trace (`Activity.traceOn`, `Activity.trace`, or a tracer's `.Trace`) tees
  annotations onto the active span as `axial.flow.annotation.*` tags, and custom sinks
  (`Flow.addAnnotationSink`) can route the same values into log scopes or anywhere else. `Flow.withTraceId id` sets
  the standard `trace_id` annotation for a flow, and `Flow.traceId` reads it back. See the
  [runtime operations tutorial](/platforms-and-hosting/runtime-operations.html).
- **Fiber ids link spans.** Workflow spans and fiber spans both carry `axial.flow.fiber.id`
  (and fiber spans `axial.flow.fiber.parent_id`), so fiber-lifecycle spans correlate with the workflows that
  forked them even when they are not parent/child in the trace tree.

## Logs

Logging goes through the runtime log sink, so applications and tests substitute the destination with `Flow.withLogSink` and no environment member.

- Workflows log through `Log.info`/`Log.error`/`Log.errorExn`/... against the runtime's current `ILogSink`.
- [`Axial.Hosting`](/hosting/dotnet.html) installs a Microsoft-backed log sink at the root of a hosted application, sending entries to
  `Microsoft.Extensions.Logging`, exceptions included, so entries flow into the host's providers.
- `FiberLogging.observe logger` is the logging counterpart of `FiberTelemetry.observe`: fiber defects are
  logged as errors, unobserved defects as critical entries. Compose both from one edge install.

OpenTelemetry can also export MEL logs (`builder.Logging.AddOpenTelemetry(...)`), which pairs naturally with
the bridge: log sink → MEL → OTLP.

## Metrics

`FiberMetrics.observe` (in `Axial.Telemetry`) records fiber starts, live counts, settlement, duration, and unobserved
defects onto the `Axial` `Meter`. Subscribe with `.AddMeter("Axial")`: a climbing `fibers.live` with flat
`fibers.settled` is a fiber leak, and a nonzero `unobserved_defects` rate is crashing background work nobody joins.
The instrument-by-instrument table is in the [FiberMetrics reference](/telemetry/api/Axial.Telemetry.FiberMetrics.html).

## Fiber dumps

A `FiberRegistry` (core `Axial`) tracks every live fiber below one edge install and answers "what is my runtime doing
right now?" with `registry.DumpAt(clock)` (a rendered tree) or `registry.Snapshot()` (structured values). The method
inventory — `Settled`, `Stats`, `UnobservedDefects`, `StartedCount`, `Interrupt`, `InterruptByName`, and the capacity
bound — is in the [FiberRegistry reference](/api/Axial.FiberRegistry.html). `FiberDumpTelemetry.record` attaches a
dump to the current trace.

## Packaging and platform notes

**Why is `Axial.Telemetry` a separate package?** It is the only piece with a dependency beyond core
(`System.Diagnostics.DiagnosticSource`) and the only piece that is meaningless off .NET. Keeping it out of
`Axial` keeps the core dependency-free and Fable-compilable; keeping it out of `Axial.Hosting`
keeps tracing available to console scripts and workers that never touch the generic host. The seams follow
the signals: core owns the neutral channels (annotations, `FiberObserver`), Telemetry turns them into spans,
Hosting turns them into MEL logs.

**Fable / JavaScript.** `System.Diagnostics.Activity` does not exist in JavaScript, so
`Axial.Telemetry` (and `Axial.Hosting`) are .NET-only. The JavaScript counterpart is
`Axial.Telemetry.JavaScript`, which emits through OpenTelemetry JS instead, in Node and the browser
alike, with the same span semantics and `axial.flow.*` tag vocabulary. It never imports the npm module
itself: the application registers the OpenTelemetry JS SDK (exporter and context manager) and hands the
`@opentelemetry/api` object to `Otel.installNamed` once at the edge, the same host/library split as registering an
application `ActivitySource` on .NET. See the [JavaScript guide](/telemetry/javascript.html) for the
bootstrap and cross-stack propagation notes.

## Where to go deeper

- [Telemetry](/telemetry/index.html): span tag vocabulary, `Activity.traceWith`,
  span-per-fiber details, metrics, dumps, and the Aspire walkthrough.
- [Supervision and fiber observability](/concurrency-and-state/supervision.html):
  `FiberObserver`, `Flow.supervise`, unobserved-defect semantics.
- [Hosting](/hosting/dotnet.html): DI integration and the log-sink/MEL bridge.
- [Runtime operations tutorial](/platforms-and-hosting/runtime-operations.html): annotations,
  timeout, retry, cancellation in practice.
