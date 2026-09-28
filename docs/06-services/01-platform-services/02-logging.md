---
title: Logging
description: Level-based logging as a declared dependency, and where it sits relative to telemetry.
---

`ILog` is a deliberately small logging contract: write a message at a level, or write one carrying an exception.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

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

```fsharp
let everyLevel (error: exn) : Flow<#IHasLog, Never, unit> =
    flow {
        do! Log.trace "trace"
        do! Log.debug "debug"
        do! Log.info "info"
        do! Log.warning "warning"
        do! Log.error "error"
        do! Log.critical "critical"

        do! Log.errorExn error "error with exception"
        do! Log.criticalExn error "critical with exception"
        do! Log.log LogLevel.Information "at a chosen level"
        do! Log.logException LogLevel.Warning error "exception at a chosen level"
    }
```

`logException` exists so the host logger receives the exception object itself, which is what preserves the stack
trace in a structured logging backend.

## Supplying the service

`Log.live` is a **no-op logger**. That is the deliberate default: a library that logs should not start writing to
somebody's console because they forgot to configure a sink. Wire a real one with `Log.fromSink`:

```fsharp
let consoleLog = Log.fromSink (fun level message -> Console.Error.WriteLine $"[{level}] {message}")
```

`Log.fromSink` appends the exception text to the message for `logException`. To hand the exception object to a real
logging framework, implement `ILog` directly and forward both members.

`Log.layer` provides the no-op logger; supply your own layer when the application has a sink.

## Logging against telemetry

`ILog` is the service a workflow depends on to say something. Spans,
metrics, and OpenTelemetry export are covered separately, in [observability](/observability/index.html). The two meet at the
host: an `ILog` implementation can forward into the same backend the telemetry exporter writes to.

Choose `ILog` when the workflow itself should emit a message. Choose telemetry when you want the runtime's own
execution structure recorded.

## Testing

Assert on log output by collecting it:

```fsharp
type LogEnv =
    { Log: ILog }
    interface IHasLog with
        member this.Log = this.Log
```

```fsharp run
let messages = ResizeArray<LogLevel * string>()
let collecting = { Log = Log.fromSink (fun level message -> messages.Add(level, message)) }

recordAttempt "invoice 7" |> Flow.run collecting |> shouldEqual (Exit.Success())
List.ofSeq messages |> shouldEqual [ LogLevel.Information, "Processing invoice 7" ]

messages.Clear()
everyLevel (InvalidOperationException "boom") |> Flow.run collecting |> ignore
messages.Count |> shouldEqual 10
```

Because `Log.live` is a no-op, a test that does not care about logging can supply it and see nothing.
