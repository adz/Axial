---
title: Axial.Telemetry
linkTitle: Telemetry
---

# Trace workflows and inspect them in Aspire

Telemetry answers three different questions:

- **What happened?** A trace shows the spans that ran and how long each one took.
- **Why did it fail?** Span status and attributes distinguish typed failures, defects, and interruption.
- **What was still running?** Fiber metrics and dumps expose background work that an ordinary task trace can miss.

Axial publishes standard .NET `ActivitySource` spans and `Meter` instruments. OpenTelemetry collects those signals and
exports them to a backend. The [.NET Aspire dashboard](#view-axial-in-the-aspire-dashboard) is the fastest way to see the
result locally.

Use `Axial.Telemetry` on .NET. For Node and browser applications compiled with Fable, use
[Axial.Telemetry.JavaScript](javascript.html). Both adapters read the same ambient `Context` and emit the same
`axial.flow.*` vocabulary.

## Before you begin

Install the .NET adapter and the OpenTelemetry host packages:

```bash
dotnet add package Axial.Telemetry
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
```

You need an OTLP receiver such as the Aspire dashboard, an OpenTelemetry Collector, Jaeger, Tempo, or a hosted
observability service. Axial does not choose an exporter or send telemetry by itself. The runnable
[Axial.ReferenceApp](https://github.com/adz/Axial/tree/main/examples/Axial.ReferenceApp) configures the SDK and OTLP
exporters, launches a local Aspire dashboard, and generates traces, metrics, logs, and a fiber dump from one endpoint.

## Configure OpenTelemetry once

Create an application-owned `ActivitySource`, then subscribe to both that source and Axial's runtime source at the host
boundary:

```fsharp no-check reason="Host builder and OpenTelemetry package setup are application-specific"
open System.Diagnostics

let applicationActivitySource = new ActivitySource("Checkout.Api")

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(fun resource ->
        resource.AddService("checkout-api") |> ignore)
    .WithTracing(fun tracing ->
        tracing
            .AddSource(applicationActivitySource.Name, "Axial")
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter()
        |> ignore)
    .WithMetrics(fun metrics ->
        metrics
            .AddMeter("Axial")
            .AddOtlpExporter()
        |> ignore)
|> ignore
```

These names answer different questions:

- `checkout-api` is the **service name**. Aspire groups telemetry by the deployed application or service that produced it.
- `Checkout.Api` is the application's **instrumentation scope**. Workflow spans describing checkout behavior should use
  this source rather than appearing to be operations owned by Axial.
- `Axial` is Axial's **runtime instrumentation scope**. Automatic fiber spans and fiber metrics remain here because they
  describe the Flow runtime.
- `checkout.submit` is the **span name** for one operation.

An `ActivitySource` only publishes spans. The OpenTelemetry SDK listens, samples, and exports them. Register every source
used by `Activity.traceOn` or `Activity.traceWithSource`; otherwise `.NET` returns `null` from `StartActivity` and the
workflow runs without a span.

## Trace a workflow

Wrap a workflow at a boundary that has an operational meaning. Pass the application source because this span describes
user code; Axial supplies the Flow-aware tracing behavior around it:

```fsharp no-check reason="The checkout workflow and domain error are application-specific"
open Axial.Telemetry

checkout order
|> Activity.traceWithSource applicationActivitySource CheckoutError.describe "checkout.submit"
```

`Activity.traceWithSource` accepts the source and a renderer for the workflow's typed error. Use `Activity.traceOn` when
`string` is an acceptable representation:

```fsharp no-check reason="The checkout workflow is application-specific"
checkout order
|> Activity.traceOn applicationActivitySource "checkout.submit"
```

`Activity.trace` and `Activity.traceWith` remain shortcuts using Axial's default source. They are useful for small
programs and compatibility, but application-owned sources produce clearer instrumentation-scope labels in Aspire and
other backends.

The span starts when the workflow runs and stops when its asynchronous execution settles. Axial records:

| Exit | Span status | Attributes |
| --- | --- | --- |
| success | `Ok` | `axial.flow.outcome = success` |
| typed failure | `Error` | `axial.flow.outcome = fail`, `axial.flow.error` |
| defect | `Error` | `axial.flow.outcome = die`, `exception.*` |
| interruption | unset | `axial.flow.outcome = interrupt`, `axial.flow.interrupted = true` |
| composite cause | dominant outcome | `axial.flow.cause` with the rendered cause tree |

Do not trace every combinator. Trace operations that you would search for in an incident: `checkout.submit`,
`invoice.generate`, or `outbox.deliver`.

## Add attributes

An attribute adds searchable context to the active span. Axial stores attributes in an immutable ambient `Context`.
The context is separate from the workflow environment because it is execution metadata, not an application service.
Nested scopes restore the previous value when they finish, and forked fibers inherit the context present at the fork.

Use a curated OpenTelemetry helper for a common semantic attribute:

```fsharp no-check reason="The checkout workflow and user value are application-specific"
checkout order
|> Context.withEndUserId user.Id
|> Activity.traceWithSource applicationActivitySource CheckoutError.describe "checkout.submit"
```

`enduser.id` is an OpenTelemetry semantic-convention attribute, currently marked **development** by OpenTelemetry. It
can contain identifying information; use a stable opaque identifier only when your privacy policy permits exporting it.
Axial documents the stability and scope of every convention for which it provides a convenience helper. Configure resource attributes such as service identity in the OpenTelemetry SDK instead of
copying them onto every span.

Define typed keys for application attributes:

```fsharp
module CheckoutAttributes =
    let tenantId = AttributeKey.string "example.tenant.id"
    let retryCount = AttributeKey.int64 "example.checkout.retry_count"
```

Attach values without boxing or runtime conversion:

```fsharp no-check reason="The checkout workflow and application values are defined elsewhere"
checkout order
|> Context.withAttributes [
    Context.attribute CheckoutAttributes.tenantId tenantId
    Context.attribute CheckoutAttributes.retryCount 2L
]
|> Activity.traceWithSource applicationActivitySource CheckoutError.describe "checkout.submit"
```

A typed key prevents attaching an integer to a string attribute. Supported values are strings, Booleans, 64-bit
integers, floating-point values, and homogeneous lists of those types.

Build a context once when several workflows share the same metadata:

```fsharp no-check reason="Request values are supplied by the application boundary"
let requestContext =
    Context.empty
    |> Context.addEndUserId user.Id
    |> Context.add (Context.attribute CheckoutAttributes.tenantId tenantId)

application
|> Context.withContext requestContext
```

Read the currently scoped context from a workflow when an integration needs to inspect it:

```fsharp no-check reason="The integration-specific export function is defined elsewhere"
flow {
    let! telemetryContext = Context.current
    return exportContext telemetryContext
}
```

Most workflows should scope attributes rather than read the whole context. Adapters use `Context.current` at integration
boundaries; application dependencies still belong in the Flow environment.

Attribute names are contracts with dashboards and alerts. Follow OpenTelemetry semantic conventions when one applies.
Use an application-owned prefix otherwise. Do not attach secrets, unrestricted personal information, or high-cardinality
values to metrics. A correlation value that must cross process boundaries may belong in OpenTelemetry baggage; trace
identity itself belongs in trace and span context, not in a duplicate attribute.

## Observe fibers

A successful root workflow can still have failed background work. Install fiber telemetry once around the application:

```fsharp no-check reason="The application workflow is defined elsewhere"
application
|> FiberTelemetry.observe
```

This records defect spans for failed fibers and for defects that no code can observe. To create a span for every forked
fiber, use `FiberTelemetry.observeWithSpans`:

```fsharp no-check reason="The application workflow is defined elsewhere"
application
|> FiberTelemetry.observeWithSpans
```

Span-per-fiber mode provides more detail and more data. Start with defect-only observation, then enable span-per-fiber
when you need fork-to-settle timing or a complete concurrency tree.

Add runtime metrics independently:

```fsharp no-check reason="The application workflow is defined elsewhere"
application
|> FiberMetrics.observe
|> FiberTelemetry.observe
```

The `Axial` meter records starts, live fibers, settlements, duration, and unobserved defects. A rising live-fiber count
without matching settlements indicates stuck or leaked work.

## Capture a fiber dump

A trace explains completed and timed operations. A fiber dump shows the work that is live now.

Install a registry at the application edge:

```fsharp no-check reason="The application workflow is defined elsewhere"
let registry = FiberRegistry()

let observedApplication =
    application
    |> Flow.withFiberRegistry registry
    |> FiberMetrics.observe
    |> FiberTelemetry.observe
```

Name long-lived work so the dump is readable:

```fsharp no-check reason="The worker workflow is application-specific"
Flow.forkNamed "outbox-poller" pollOutbox
```

Return `registry.Dump()` from a protected diagnostics endpoint, write it during a stuck shutdown, or attach it to the
current trace:

```fsharp no-check reason="The registry is installed at the application edge"
FiberDumpTelemetry.record registry
```

`FiberDumpTelemetry.record` adds an `axial.flow.fiber.dump` event to the current activity. If no activity is current, it
creates a standalone span. This connects a slow or failed trace to the exact fibers that were running when you captured
the dump. See [Supervision and fiber observability](/concurrency-and-state/supervision.html) for registry and dump details.

## View Axial in the Aspire dashboard

Aspire's dashboard accepts OTLP telemetry. The quickest complete example is
[Axial.ReferenceApp](https://github.com/adz/Axial/tree/main/examples/Axial.ReferenceApp): run
`dotnet run --file apphost.cs` from its directory, then call its `/observability/demo` endpoint as described in the
example README.

If your Aspire service uses `AddServiceDefaults()`, keep that setup and add the application source, Axial's runtime
source, and Axial's meter:

```fsharp no-check reason="Aspire service-default wiring is application-specific"
builder.Services
    .AddOpenTelemetry()
    .WithTracing(fun tracing ->
        tracing.AddSource(applicationActivitySource.Name, "Axial") |> ignore)
    .WithMetrics(fun metrics -> metrics.AddMeter("Axial") |> ignore)
|> ignore
```

Run the application and open the dashboard:

1. Open **Traces** and select an incoming request.
2. Expand the request span to find `checkout.submit` or another `Activity.trace` span.
3. Inspect its status and attributes, including `enduser.id`, application attributes, and `axial.flow.outcome`.
4. Enable `FiberTelemetry.observeWithSpans` to see named child fibers in the same trace.
5. Trigger `FiberDumpTelemetry.record` and inspect the `axial.flow.fiber.dump` event on the active span.
6. Open **Metrics** and chart `axial.flow.fibers.live` and `axial.flow.fibers.unobserved_defects`.

This is the intended feedback loop: traces identify the slow or failed operation, attributes identify its application
context, metrics show whether the runtime is degrading, and a fiber dump shows the work still in flight.

## Choose attributes, annotations, or logs

- Use **telemetry context attributes** for values intentionally exported to tracing backends and queried across spans.
- Use `Flow.annotate` for Axial runtime diagnostics. Telemetry exports these under
  `axial.flow.annotation.*`, preserving their separate namespace.
- Use `ILog` for messages and exceptions that belong in the host logging pipeline.

The mechanisms share ambient scoping, but they are different operational contracts.

## Next steps

- [JavaScript telemetry](javascript.html)
- [Telemetry API summary](reference.html)
- [Observability overview](/observability/index.html)
- [Supervision and fiber observability](/concurrency-and-state/supervision.html)
