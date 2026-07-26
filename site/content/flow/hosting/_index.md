---
title: .NET Hosting
linkTitle: .NET
weight: 80
description: Standalone process, Generic Host, dependency injection, and Microsoft logging integration.
type: docs
---


`Axial.Flow.Hosting` connects root Flow applications to .NET process and Generic Host lifecycle. It also adapts
`Microsoft.Extensions.Logging` to the explicit `ILog` service and provides fiber-defect logging.

```sh
dotnet add package Axial.Flow.Hosting
```

The package is optional. [`App`](../applications/) works in console, desktop, test, and embedded applications without
Microsoft.Extensions.Hosting or a dependency-injection container.

## Standalone CLI or Script

Use `DotNetApp.run` when the application owns a console process but does not use Generic Host:

```fsharp
open Axial.Flow
open Axial.Flow.Hosting

type AppError = | InvalidArguments of string

let describeError = function
    | InvalidArguments message -> message

let application : Flow<string array, AppError, unit> =
    flow {
        let! args = Flow.env<string array, AppError>
        if Array.isEmpty args then
            return! Flow.fail (InvalidArguments "Supply at least one argument.")
    }

[<EntryPoint>]
let main args =
    DotNetApp.run describeError args application
        .GetAwaiter()
        .GetResult()
```

`DotNetApp.run` installs a temporary `Console.CancelKeyPress` handler. Ctrl+C requests `App.Stop()`, waits for root
scope cleanup, removes the handler, and returns:

| Exit | Process code |
| --- | ---: |
| Success | `0` |
| Typed failure only | `1` |
| Defect | `2` |
| Interruption | `130` |

A cleanup defect takes precedence when a cause also contains interruption.

It returns the code rather than calling `Environment.Exit`, so `finally` blocks and asynchronous finalizers are not
skipped.

## Generic Host

Build the application as a `Flow` whose input is either `IServiceProvider` or an explicit environment constructed
from it:

```fsharp
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Axial.Flow
open Axial.Flow.Hosting

let builder = Host.CreateApplicationBuilder(args)

builder.Services.AddSingleton<IOrderRepository, SqlOrderRepository>()
|> ignore

let application : Flow<AppEnv, AppError, unit> =
    program
    |> Flow.provide Live.appLayer

builder.Services
|> Hosting.addApp
    (fun services ->
        { Orders = services.GetRequiredService<IOrderRepository>() })
    AppError.describe
    application
|> ignore

builder.Build().Run()
```

The registered `FlowHostedService`:

1. Constructs the explicit environment when the host starts.
2. Starts one root `App`.
3. Connects `IHostApplicationLifetime.ApplicationStopping` to coordinated stop.
4. Logs success, typed failure, interruption, or defects through the host logger.
5. Requests host shutdown when the finite root application completes.
6. Waits for root cleanup from `StopAsync`.

Use `Hosting.addAppWith { StopHostOnCompletion = false }` when the root Flow is one hosted participant and another
hosted service owns process completion.

`IServiceProvider` stays at the application edge. Prefer constructing an explicit record or providing a `Layer` before
domain workflows run. See [Service-provider boundaries](../services-and-runtimes/service-provider-boundaries/).

## Microsoft Logging as `ILog`

Create the explicit Axial logging service from an existing logger:

```fsharp
let axialLog : ILog =
    MicrosoftLogging.create logger
```

Or choose a category through a factory:

```fsharp
let axialLog : ILog =
    MicrosoftLogging.fromFactory "MyApp" loggerFactory
```

`MicrosoftLogging.layer "MyApp"` provisions `ILog` from an `ILoggerFactory` layer input. All Axial log levels and
exception objects are preserved. The adapter never silently substitutes a no-op logger.

## Fiber Defect Logging

Install the observer once around the root application:

```fsharp
let observed =
    application
    |> FiberLogging.observe logger
```

Fiber defects are errors; unobserved fiber defects are critical entries. Compose it with telemetry when both are
required:

```fsharp
application
|> Flow.withFiberObserver
    (FiberObserver.compose
        FiberTelemetry.observerWithSpans
        (FiberLogging.observer logger))
```

Logging is an explicit application dependency; telemetry remains runtime instrumentation. See
[Observability](../observability/).

## Desktop and Embedded Applications

Desktop frameworks already own application lifetime. Start `App` after startup and await stop from the framework's
closing path:

```fsharp
let running = App.start environment application

let closeApplication () = async {
    let! _ = running.Stop()
    dispatcher.RequestExit()
}
```

Do not block the UI thread on `Completion`. Framework-specific packages are unnecessary unless an integration can
provide more than wiring one close event to `Stop()`.

## Other Runtimes

- [Node hosting](./node/) handles Node arguments, `process.env`, signals, and exit status.
- [Browser hosting](./browser/) handles explicit UI ownership and `AbortSignal`.
