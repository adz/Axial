---
title: Logging
description: Level-based logging as a declared dependency, and where it sits relative to telemetry.
---

`ILog` is a deliberately small logging contract: write a message at a level, or write one carrying an exception.

```fsharp
open System
open Axial
open Axial.PlatformService
```

```fsharp
let recordAttempt name : Flow<#IHasLog, Never, unit> =
    Log.info $"Processing {name}"
```

Each level has a helper, and two more carry an exception without flattening its stack trace into a string:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
Log.trace message
Log.debug message
Log.info message
Log.warning message
Log.error message
Log.critical message

Log.errorExn error message
Log.criticalExn error message
Log.log level message
Log.logException level error message
```

`logException` exists so the host logger receives the exception object itself, which is what preserves the stack
trace in a structured logging backend.

## Supplying the service

`Log.live` is a **no-op logger**. That is the deliberate default: a library that logs should not start writing to
somebody's console because they forgot to configure a sink. Wire a real one with `Log.fromSink`:

```fsharp
let log = Log.fromSink (fun level message -> printfn $"[{level}] {message}")
```

`Log.fromSink` appends the exception text to the message for `logException`. To hand the exception object to a real
logging framework, implement `ILog` directly and forward both members.

`Log.layer` provides the no-op logger; supply your own layer when the application has a sink.

## Logging against telemetry

`ILog` is the service a workflow depends on to say something. It is not the tracing and metrics story — spans,
metrics, and OpenTelemetry export are covered in [observability](/observability/index.html). The two meet at the
host: an `ILog` implementation can forward into the same backend the telemetry exporter writes to.

Choose `ILog` when the workflow itself should emit a message. Choose telemetry when you want the runtime's own
execution structure recorded.

## Testing

Assert on log output by collecting it:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let messages = ResizeArray<LogLevel * string>()
let log = Log.fromSink (fun level message -> messages.Add(level, message))
```

Because `Log.live` is a no-op, a test that does not care about logging can supply it and see nothing.
