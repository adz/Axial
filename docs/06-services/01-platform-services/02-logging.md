---
title: Logging
description: Scoped log sinks, level gates, metadata and host integration.
---

`Log.info` and the other helpers work in any environment. They write to the runtime's `ILogSink`, not a service in
an application record. The default writes Information and above to stderr; it does not write to stdout.
Use `LogSink.none` to silence a test or `Log.memory ()` to record entries.

```fsharp prepare
open System
open Axial
open Axial.PlatformService
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
let recordAttempt name : Flow<'env, Never, unit> = Log.info $"Processing {name}"

let everyLevel (error: exn) : Flow<'env, Never, unit> =
    flow {
        do! Log.trace "trace"
        do! Log.debug "debug"
        do! Log.info "info"
        do! Log.warning "warning"
        do! Log.error "error"
        do! Log.critical "critical"
        do! Log.errorExn error "error with exception"
        do! Log.criticalExn error "critical with exception"
        do! Log.log LogLevel.Information "chosen level"
        do! Log.logException LogLevel.Warning error "chosen exception level"
    }
```

Exceptions remain objects in `LogEntry.Error`, preserving their stack and identity. Each entry also contains
`Level`, `Message`, an `Annotations` map and a snapshot of explicitly set named `FiberLocal` values in `Locals`.
`ILogSink.IsEnabled` is checked before building the entry; a disabled call builds no record or metadata snapshot.
Exceptions thrown by a sink's gate or writer are guarded and cannot change the flow's outcome.

## Replace or compose a sink

`Flow.withLogSink sink` replaces the destination within its subtree. Descendant fibers inherit it, and leaving the
scope restores the outer sink. `Flow.mapLogSink decorate` instead transforms the destination already in force.

| Combinator | Behavior |
| --- | --- |
| `LogSink.none` | Disabled destination |
| `LogSink.fromFunction write` | Calls a function with each enabled `LogEntry` |
| `LogSink.console minimum` | Information-style level prefix on stderr at a chosen minimum |
| `LogSink.minLevel minimum sink` | Rejects lower levels before entry creation |
| `LogSink.filter predicate sink` | Filters complete entries, after they are built |
| `LogSink.map transform sink` | Transforms entries and checks the mapped level against the destination gate |
| `LogSink.tee first second` | Sends enabled entries to both guarded destinations |

`Log.live` is the default console sink, not a no-op. `Log.none` is a no-op, and `Log.fromSink` accepts a
`LogEntry -> unit` function. These are convenience values; there is no logging layer to put into `'env`.

```fsharp run
let first = Log.memory ()
let second = Log.memory ()
let combined = LogSink.tee first second |> LogSink.minLevel LogLevel.Information
let workflow: Flow<unit, Never, unit> = flow {
    do! Log.debug "disabled"
    do! recordAttempt "invoice 7"
    let! child = Log.info "child" |> Flow.fork
    do! Fiber.join child
}
workflow |> Flow.withLogSink combined |> Flow.annotate "request" "req-7" |> Flow.run () |> shouldEqual (Exit.Success ())
first.Entries |> Seq.map _.Message |> Seq.toList |> shouldEqual ["Processing invoice 7"; "child"]
second.Entries |> Seq.forall (fun entry -> entry.Annotations.["request"] = "req-7") |> shouldEqual true
first.Clear()
everyLevel (InvalidOperationException "boom") |> Flow.withLogSink first |> Flow.run () |> ignore
first.Entries.Count |> shouldEqual 10
```

`TestLog.Entries` returns a thread-safe snapshot; `Clear()` removes subsequent recorded state, not earlier snapshots.
`Flow.runTest` installs a fresh recording log automatically; `TestPlatform.create ()` and `Flow.runTestWith` expose
that instance for assertions. `Flow.mapLogSink (LogSink.tee auditSink)` adds a second destination to just one subtree.

## Host adapters

- Generic Host installs a Microsoft logging sink at the root, category `Axial`.
- ASP.NET Core installs one per `flowEndpoint` request, category `Axial.Endpoint`.
- GenHTTP has no logging abstraction; `EndpointOptions.LogSink` is optional and otherwise leaves the runtime default.
- Node installs `NodeLogging.sink LogLevel.Information` through `console.error` on stderr. Browser entry points use
  `BrowserLogging.sink`, mapping levels to `console.debug/info/warn/error`. Both preserve exception and metadata
  arguments, and treat percent directives in message text literally.

Host options replace or omit installation; an inner `Flow.withLogSink` always wins. `None` leaves the runtime
default, while a supplied `LogSink.none` silences logging.

`MicrosoftLogging.sink logger` and `MicrosoftLogging.fromFactory category factory` live in `Axial.Hosting`.
They use the host's level gate and map annotations plus named locals to a `BeginScope` dictionary. No empty scope
is opened when both are empty. Providers must enable scope capture to display those fields.

```fsharp no-check reason="The caller supplies the Microsoft ILoggerFactory at its host composition boundary"
open Axial
open Axial.Hosting
let hosted factory application =
    application |> Flow.withLogSink (MicrosoftLogging.fromFactory "App" factory)
```

`FiberLogging.observe logger` remains a separate, opt-in observer for defects. Installing a host log sink does not
silently enable it. See the [.NET hosting guide](/platforms-and-hosting/dotnet.html).

## Logging and telemetry

Logging emits application messages. Spans, metrics and typed attributes describe execution structure; see
[observability](/observability/index.html). `Flow.withTelemetry` replaces both `ITelemetry` observation callbacks
for a subtree. The Activity and JavaScript OpenTelemetry adapters tee through the existing callbacks; neither
installs a log sink. Compose destinations explicitly when they should share a backend.
