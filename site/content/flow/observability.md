---
weight: 100
title: Observability
description: How tracing, logging, and metrics fit together across Axial packages, and how to plug in OpenTelemetry.
type: docs
---


This page is the map: what each observability signal is, which Axial package produces it, what you get
automatically versus what you opt into, and the one-time wiring that sends it all to a backend such as
OpenTelemetry. The fuller guides are linked from each section.

| Signal | Where it comes from | Consumed by |
| --- | --- | --- |
| **Traces** (spans) | [`Axial.Flow.Telemetry`]({{< relref "/flow/telemetry/_index.md" >}}) emitting on the `Axial.Flow` `ActivitySource`; `Axial.Flow.Telemetry.JavaScript` on Fable targets | any `ActivityListener` — in practice the OpenTelemetry SDK; OpenTelemetry JS under Fable |
| **Logs** | the explicit `ILog` service, bridged to `Microsoft.Extensions.Logging` by [`Axial.Flow.Hosting`]({{< relref "/flow/hosting/_index.md" >}}) | your host's logging pipeline |
| **Metrics** | [`Axial.Flow.Telemetry`]({{< relref "/flow/telemetry/_index.md" >}}) — `FiberMetrics` on the `Axial.Flow` `Meter` | OpenTelemetry's `.AddMeter("Axial.Flow")`, `dotnet-counters`, the Aspire dashboard |
| **Fiber dumps** | core `Axial.Flow` — `FiberRegistry` live-fiber snapshots, no telemetry dependency | `registry.Dump()` on demand; `FiberDumpTelemetry.record` to put dumps on traces |

Two general-purpose channels feed those signals and are part of core `Axial.Flow`, not the telemetry
package: **runtime annotations** (`Flow.annotate`, ambient key–value diagnostics metadata) and **fiber
observers** (`FiberObserver`, lifecycle hooks for every forked fiber — see
[Supervision and fiber observability]({{< relref "/flow/concurrency/supervision.md" >}})).

## How .NET tracing works: `ActivitySource` and `ActivityListener`

.NET has a built-in publish/subscribe tracing model in `System.Diagnostics`, and Axial sits entirely on the
publishing side:

- An **`ActivitySource`** is the producer. Axial owns one, named `"Axial.Flow"`. Instrumented code calls
  `StartActivity`, and an `Activity` is a span: name, timing, tags, status, parent.
- An **`ActivityListener`** is the consumer. Nothing is recorded until the application registers a listener
  that opts into a source by name and makes the sampling decision. With no interested listener,
  `StartActivity` returns `null` and Axial skips all tagging work — an untraced app pays roughly a null
  check per span site.
- **`Activity.Current`** is an async-local holding the ambient span. New spans parent to it automatically,
  which is how Axial spans nest inside ASP.NET Core request spans (and under an upstream `traceparent`
  header) with no wiring.

This split is why telemetry is runtime instrumentation rather than an environment service: the host decides
once, at the edge, whether anything listens and where spans go; workflows never carry a tracing dependency.

## What produces spans, and when

Tracing is **explicit at workflow granularity**. Axial does not span every `flow { }` or operator — a span
exists where you put one:

```fsharp
open Axial.Flow.Telemetry

let placeOrder order =
    flow { (* validate, charge, persist *) }
    |> Activity.trace "orders.place"
```

`Activity.trace` stamps the span with environment identity traits (`IHasRequestId`,
`IHasCorrelationId`, `IHasTenantId`, `IHasTelemetryTags`), the fiber id, every runtime annotation, and — when
the workflow settles, so the duration covers asynchronous work — the exit outcome and error/defect tags. The
full tag vocabulary is documented on the [Telemetry page]({{< relref "/flow/telemetry/_index.md" >}}).

What you get without per-callsite work:

- **Fiber observability** — one edge install of `FiberTelemetry.observe` records a span for every fiber
  defect and every provably unobserved defect anywhere below it; `FiberTelemetry.observeWithSpans` upgrades
  every forked fiber to a real span covering fork to settle.
- **Host and client spans** — ASP.NET Core, `HttpClient`, and database instrumentation span their own
  boundaries. Axial spans nest inside them via `Activity.Current`, so in a web application every request is
  already a trace; `Activity.trace` adds the meaningful interior structure.

## Plugging in OpenTelemetry

Because Axial emits through a standard `ActivitySource`, the OpenTelemetry SDK is the listener — there is no
adapter to write. Subscribe to the source name `"Axial.Flow"` once at the application edge.

In an ASP.NET Core or Generic Host application:

```fsharp
// dotnet add package OpenTelemetry.Extensions.Hosting
// dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
// dotnet add package OpenTelemetry.Instrumentation.AspNetCore

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(fun resource -> resource.AddService("my-app") |> ignore)
    .WithTracing(fun tracing ->
        tracing
            .AddSource("Axial.Flow")          // subscribe to Axial's spans
            .AddAspNetCoreInstrumentation()   // incoming request spans
            .AddOtlpExporter()                // collector, Jaeger, Tempo, Honeycomb, ...
        |> ignore)
|> ignore
```

In a console application or script, build the provider directly and keep it alive for the process lifetime:

```fsharp
open OpenTelemetry
open OpenTelemetry.Resources
open OpenTelemetry.Trace

use tracerProvider =
    Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("my-script"))
        .AddSource("Axial.Flow")
        .AddOtlpExporter()   // or .AddConsoleExporter() to print spans locally
        .Build()
```

Then install the edge observers on your application workflow — this is Axial code you want with or without
an exporter attached:

```fsharp
open Axial.Flow.Hosting
open Axial.Flow.Telemetry

application
|> Flow.withFiberObserver
    (FiberObserver.compose FiberTelemetry.observerWithSpans (FiberLogging.observer logger))
```

Sampling is the host's knob: the SDK samples everything by default, and something like
`.SetSampler(TraceIdRatioBasedSampler 0.1)` scales that back in production. When the sampler declines,
Axial's `StartActivity` returns `null` and the span site costs almost nothing.

For a quick local look without infrastructure, use `.AddConsoleExporter()`, or run
`docker run -p 4317:4317 -p 16686:16686 jaegertracing/all-in-one` and browse traces at `localhost:16686`.

## Correlation: how the signals join up

- **Environment traits → span tags.** `Activity.trace` reads `IHasRequestId`, `IHasCorrelationId`,
  `IHasTenantId`, and the extensible `IHasTelemetryTags` from the workflow environment and stamps them as
  tags, so spans carry your identity model without manual tagging.
- **Annotations → every observer.** `Flow.annotate "payment.attempt" attemptId` is scoped runtime metadata,
  not a tracing call: `Activity.trace` tees annotations onto the active span as
  `axial.flow.annotation.*` tags, and custom sinks (`Flow.addAnnotationSink`) can route the same values into
  log scopes or anywhere else. See the
  [runtime operations tutorial]({{< relref "/flow/tutorials/runtime-operations.md" >}}).
- **Fiber ids link spans.** Workflow spans and fiber spans both carry `axial.flow.fiber.id`
  (and fiber spans `axial.flow.fiber.parent_id`), so fiber-lifecycle spans correlate with the workflows that
  forked them even when they are not parent/child in the trace tree.

## Logs

Logging is deliberately the opposite design from tracing: *which logger* is an application dependency you
substitute, so `ILog` is an explicit environment service, not ambient instrumentation.

- Workflows log through `Log.info`/`Log.error`/`Log.errorExn`/... against `IHas<ILog>`.
- [`Axial.Flow.Hosting`]({{< relref "/flow/hosting/_index.md" >}}) bridges `ILog` to
  `Microsoft.Extensions.Logging`, exceptions included, so entries flow into the host's providers.
- `FiberLogging.observe logger` is the logging counterpart of `FiberTelemetry.observe`: fiber defects are
  logged as errors, unobserved defects as critical entries. Compose both from one edge install as shown
  above.

OpenTelemetry can also export MEL logs (`builder.Logging.AddOpenTelemetry(...)`), which pairs naturally with
the bridge: `ILog` → MEL → OTLP.

## Metrics

The .NET counterpart of `ActivitySource` is `System.Diagnostics.Metrics.Meter`, and Axial owns one, named
`"Axial.Flow"`. `FiberMetrics.observe` (in `Axial.Flow.Telemetry`) installs a fiber observer that records
runtime health onto it:

| Instrument | Kind | Meaning |
| --- | --- | --- |
| `axial.flow.fibers.started` | counter | fibers forked |
| `axial.flow.fibers.live` | up-down counter | fibers currently running |
| `axial.flow.fibers.settled` | counter, tagged `axial.flow.fiber.status` | settles split by `Succeeded`/`Failed`/`Interrupted` |
| `axial.flow.fiber.duration` | histogram (seconds), tagged with status | fork-to-settle lifetime |
| `axial.flow.fibers.unobserved_defects` | counter | defects the runtime proved no code could observe |

```fsharp
application
|> FiberMetrics.observe        // fiber runtime metrics
|> FiberTelemetry.observe      // fiber defect spans — installs compose
```

Subscribe with `.AddMeter("Axial.Flow")` in `.WithMetrics(...)` and the instruments land in any OTLP
backend. A climbing `fibers.live` with flat `fibers.settled` is a fiber leak; a nonzero
`unobserved_defects` rate is crashing background work nobody joins — signals plain `Task.Run` code cannot
give you without hand-rolled bookkeeping.

Host instrumentation (`.AddAspNetCoreInstrumentation()`, `.AddHttpClientInstrumentation()`,
`.AddRuntimeInstrumentation()`) still covers request rates and process health; the public `FiberObserver`
hooks remain available for app-specific counters on your own meter.

## Fiber dumps

A `FiberRegistry` (core `Axial.Flow`, no telemetry dependency) tracks every live fiber below one edge
install and answers "what is my runtime doing right now?" with a structured snapshot or a rendered tree:

```fsharp
let registry = FiberRegistry()

application
|> Flow.withFiberRegistry registry   // composes with observers installed elsewhere

// later — a diagnostics endpoint, a SIGQUIT-style handler, a stuck-shutdown log:
printfn "%s" (registry.Dump())
```

```text
Fiber dump @ 2026-07-16T10:00:12.5000000+00:00 — 3 live fiber(s)
#1 "outbox-supervisor" Running 3605.2s (started 2026-07-16T09:00:07.2000000+00:00)
├─ #2 "outbox-poller" Running 12.5s (started 2026-07-16T10:00:00.0000000+00:00) [tenant=acme]
└─ #3 Running 0.4s (started 2026-07-16T10:00:12.1000000+00:00)
```

Name fibers at the fork site with `Flow.forkNamed "outbox-poller" work` — the name carries into dumps,
fiber spans, and metrics-adjacent tags, so long-lived background fibers are recognizable instead of bare
ids. Each dump entry also carries the runtime annotations that were in scope at the fork site and, for
settled fibers, the settle timestamp. `registry.Snapshot()` returns the same data as structured
`FiberDump` values for programmatic checks; `Fiber.dump fiber` snapshots a single handle.

To put a dump where your traces are, `FiberDumpTelemetry.record registry` attaches the live-fiber tree to
the current activity as an `axial.flow.fiber.dump` event (or a standalone span when no activity is
current) — useful just before a timeout fires or from a slow-request handler, so the trace that explains
*that something was slow* also records *what the runtime was busy with*.

## The Aspire dashboard

Nothing Aspire-specific is required: Aspire's dashboard is an OTLP backend, and `AddServiceDefaults()` in an
Aspire service project already wires the OpenTelemetry SDK. Add the two Axial sources to the pipeline —

```fsharp
builder.Services
    .AddOpenTelemetry()
    .WithTracing(fun tracing -> tracing.AddSource("Axial.Flow") |> ignore)
    .WithMetrics(fun metrics -> metrics.AddMeter("Axial.Flow") |> ignore)
|> ignore
```

— and the dashboard shows:

- **Traces**: workflow spans from `Activity.trace`, and with `FiberTelemetry.observeWithSpans` a span per
  forked fiber (named fibers display as `axial.flow.fiber <name>`), nested under the ASP.NET Core request
  span. Fiber dump events from `FiberDumpTelemetry.record` appear on the span that recorded them.
- **Metrics**: the `axial.flow.fibers.*` instruments as live charts — watch `fibers.live` breathe under
  load, and alarm on `unobserved_defects`.
- **Structured logs**: fiber defects via `FiberLogging.observe` through the `ILog`/MEL bridge.

## Distributed tracing across a .NET backend and a Fable frontend

The two telemetry packages join into one distributed trace through the W3C `traceparent` header. Neither
package does the propagation itself — that is the OpenTelemetry SDKs' job on both ends:

1. The browser app bootstraps OTel JS (`WebTracerProvider`, `ZoneContextManager`, an OTLP exporter, and
   `@opentelemetry/instrumentation-fetch`), then `Otel.install`.
2. A user action runs `submitOrder |> Otel.trace "orders.submit"` — a span starts and becomes the active
   context.
3. The workflow calls the backend with `fetch`; the fetch instrumentation opens a client span under it and
   injects `traceparent` into the request.
4. ASP.NET Core reads `traceparent` natively, so the request span is a remote child of the browser's; with
   `.AddAspNetCoreInstrumentation()` it is recorded.
5. The handler runs `placeOrder |> Activity.trace "orders.place"`, nesting under the request span via
   `Activity.Current`.

Both ends export to the same collector, and the trace view shows one tree —
`orders.submit → fetch → POST /orders → orders.place` — with browser and server spans interleaved. Because
both packages compile the same shared vocabulary source (`src/Axial.Flow.Telemetry.Shared`), the
`axial.flow.*` attributes mean the same thing on both halves, so one dashboard query spans the stack.

The gotchas are standard browser-OTel operations, not Axial concerns: the API must allow the `traceparent`
header through CORS and the fetch instrumentation needs `propagateTraceHeaderCorsUrls` for cross-origin
calls; browsers should export via a collector (CORS again); use parent-based sampling on the server so the
frontend's sampling decision carries through; and without a context manager, a `fetch` issued after an
awaited boundary loses the active span and starts a fresh trace.

## Packaging and platform notes

**Why is `Axial.Flow.Telemetry` a separate package?** It is the only piece with a dependency beyond core
(`System.Diagnostics.DiagnosticSource`) and the only piece that is meaningless off .NET. Keeping it out of
`Axial.Flow` keeps the core dependency-free and Fable-compilable; keeping it out of `Axial.Flow.Hosting`
keeps tracing available to console scripts and workers that never touch the generic host. The seams follow
the signals: core owns the neutral channels (annotations, `FiberObserver`), Telemetry turns them into spans,
Hosting turns them into MEL logs.

**Fable / JavaScript.** `System.Diagnostics.Activity` does not exist in JavaScript, so
`Axial.Flow.Telemetry` (and `Axial.Flow.Hosting`) are .NET-only. The JavaScript counterpart is
`Axial.Flow.Telemetry.JavaScript`, which emits through OpenTelemetry JS instead — in Node and the browser
alike — with the same span semantics and `axial.flow.*` tag vocabulary. It never imports the npm module
itself: the application registers the OpenTelemetry JS SDK (exporter and context manager) and hands the
`@opentelemetry/api` object to `Otel.install` once at the edge, the same host/library split as
`.AddSource("Axial.Flow")` on .NET:

```fsharp
open Fable.Core.JsInterop
open Axial.Flow.Telemetry.JavaScript

// after registering the OpenTelemetry JS SDK (NodeSDK / WebTracerProvider)
Otel.install (importAll "@opentelemetry/api")

application
|> Otel.trace "orders.place"      // the JS counterpart of Activity.trace
|> FiberTelemetry.observe         // fiber defect spans, as on .NET
```

Platform caveats: span parenting across awaited boundaries requires the application's OpenTelemetry context
manager (`AsyncLocalStorageContextManager` on Node, `ZoneContextManager` in the browser); environment traits
are read structurally because interface type tests are erased in JavaScript; and the GC-based
unobserved-defect net relies on .NET finalization, so under Fable unobserved defects are reported only at the
deterministic detection sites (discarded race/timeout losers and scope close). The package's .NET build is
inert — `Otel.install` throws and `Otel.trace` is a pass-through — so shared Fable/.NET source trees compile
without conditional references; on .NET, use `Axial.Flow.Telemetry`.

## Where to go deeper

- [Telemetry]({{< relref "/flow/telemetry/_index.md" >}}) — span tag vocabulary, `Activity.traceWith`,
  span-per-fiber details.
- [Supervision and fiber observability]({{< relref "/flow/concurrency/supervision.md" >}}) —
  `FiberObserver`, `Flow.Runtime.supervise`, unobserved-defect semantics.
- [Hosting]({{< relref "/flow/hosting/_index.md" >}}) — DI integration and the `ILog`/MEL bridge.
- [Runtime operations tutorial]({{< relref "/flow/tutorials/runtime-operations.md" >}}) — annotations,
  timeout, retry, cancellation in practice.
