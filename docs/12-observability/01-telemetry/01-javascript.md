---
title: JavaScript Telemetry
description: Export Flow traces from Fable applications running in Node or a browser.
platform: fable
---

# Export traces from Node or a browser

`Axial.Telemetry.JavaScript` writes spans through the JavaScript OpenTelemetry API. It remains separate from the .NET
adapter because JavaScript uses different span and context-propagation APIs. Both packages consume the same ambient
`Axial.Telemetry.Context` from core and emit the same Axial attribute names.

## Install the packages

```bash
dotnet add package Axial.Telemetry.JavaScript
npm install @opentelemetry/api
```

Install the OpenTelemetry SDK, exporter, and context manager required by your Node or browser host. Axial accepts the API
object; it does not import or configure an SDK for you.

## Install the tracer

After the host has registered its OpenTelemetry provider and context manager, pass the API object and the application's
instrumentation scope name to Axial:

```fsharp no-check reason="Fable import and host SDK registration require the application's JavaScript build"
open Fable.Core.JsInterop
open Axial.Telemetry.JavaScript

Otel.installNamed (importAll "@opentelemetry/api") "Checkout.Web"
```

`Checkout.Web` identifies the application code being traced; it is not the service name or a span name. The JavaScript
SDK separately configures the service resource, while calls such as `Otel.trace "checkout.submit"` provide span names.
`Otel.install` remains a shortcut using the default `Axial` scope, and `Otel.installWith` accepts an explicitly created
tracer.

Node applications normally use an `AsyncLocalStorageContextManager`. Browser applications commonly use a zone-based
context manager. Without a context manager, a span can lose its active parent after an awaited boundary.

## Trace and tag a workflow

The context API is the same on .NET and JavaScript:

```fsharp no-check reason="The workflow and domain values are application-specific"
open Axial.Telemetry
open Axial.Telemetry.JavaScript

checkout order
|> Context.withEndUserId user.Id
|> Context.withAttributes [
    Context.attribute AppAttributes.tenantId tenantId
]
|> Otel.traceWith CheckoutError.describe "checkout.submit"
```

`Otel.traceWith` is the JavaScript counterpart of `Activity.traceWith`. It records the same Flow outcome, typed-error,
defect, interruption, fiber, runtime-annotation, and ambient-context attributes.

## Connect browser and server traces

OpenTelemetry's HTTP instrumentation propagates the W3C `traceparent` header. To see one trace across a browser and a
.NET backend:

1. Register fetch or XMLHttpRequest instrumentation in the browser.
2. Allow `traceparent` through CORS for cross-origin requests.
3. Configure `propagateTraceHeaderCorsUrls` for the backend origins.
4. Enable ASP.NET Core instrumentation on the server.
5. Export both applications to the same collector or Aspire-compatible OTLP backend.

The browser workflow span then parents the fetch span, and the server request span continues the same trace. Axial does
not manually copy correlation identifiers; the OpenTelemetry SDKs propagate trace context.

## Observe fibers

Install defect-only observation at the application edge:

```fsharp no-check reason="The application workflow is defined elsewhere"
application
|> FiberTelemetry.observe
```

Use `FiberTelemetry.observeWithSpans` when you need one span per forked fiber. Under Fable, unobserved defects are
reported at deterministic runtime detection points. JavaScript garbage collection does not provide the same finalization
signal used by .NET.

## Verify the result

Open your trace backend and confirm that:

- the workflow span has the expected parent;
- `axial.flow.outcome` matches the workflow exit;
- `enduser.id` and custom context attributes are present;
- defects carry `exception.type`, `exception.message`, and `exception.stacktrace`;
- named fiber spans appear when span-per-fiber observation is enabled.

For the complete signal model, Aspire walkthrough, metrics, and fiber dumps, read
[Trace workflows and inspect them in Aspire](index.html). Fiber metrics and `System.Diagnostics`-based dump events are
provided by the .NET adapter; the core `FiberRegistry` and rendered dumps remain available on supported Fable hosts.
