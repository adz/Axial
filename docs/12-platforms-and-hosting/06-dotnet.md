---
title: .NET Hosting
linkTitle: .NET
description: Standalone process, Generic Host, dependency injection, and Microsoft logging integration.
platform: dotnet
targetFramework: net8.0
project: src/Axial.Hosting/Axial.Hosting.fsproj
---

# .NET Hosting

`Axial.Hosting` connects root Flow applications to .NET process and Generic Host lifecycle. It also adapts
`Microsoft.Extensions.Logging` to the explicit `ILog` service and provides fiber-defect logging.

```sh
dotnet add package Axial.Hosting
```

The package is optional. [`App`](application-lifecycle.html) works in console, desktop, test, and embedded applications without
Microsoft.Extensions.Hosting or a dependency-injection container.

## Standalone CLI or Script

Use `DotNetApp.run` when the application owns a console process but does not use Generic Host:

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
open Axial
open Axial.Hosting

type AppError = | InvalidArguments of string

let describeError = function
    | InvalidArguments message -> message

let application : Flow<string array, AppError, unit> =
    flow {
        let! args = Flow.env<string array, AppError>
        if Array.isEmpty args then
            return! Flow.fail (InvalidArguments "Supply at least one argument.")
    }

// In a program, mark this [<EntryPoint>].
let main args =
    (DotNetApp.run describeError args application).GetAwaiter().GetResult()
```

```fsharp run
main [| "orders.csv" |] |> shouldEqual 0
main [||] |> shouldEqual 1
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

```fsharp no-check reason="Needs the Microsoft.Extensions.Hosting package, which the documentation build does not reference"
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Axial
open Axial.Hosting

let builder = Host.CreateApplicationBuilder(args)

builder.Services.AddSingleton<IOrderRepository, SqlOrderRepository>()
|> ignore

let application : Flow<AppEnv, AppError, unit> =
    program
    |> Layer.provide Live.appLayer

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
domain workflows run. See [Providing the environment](/dependencies/providing-the-environment.html).

## Microsoft Logging as `ILog`

Create the explicit Axial logging service from an existing logger:

```fsharp
let axialLogFor (logger: Microsoft.Extensions.Logging.ILogger) : ILog =
    MicrosoftLogging.create logger
```

Or choose a category through a factory:

```fsharp
let axialLogFrom (loggerFactory: Microsoft.Extensions.Logging.ILoggerFactory) : ILog =
    MicrosoftLogging.fromFactory "MyApp" loggerFactory
```

`MicrosoftLogging.layer "MyApp"` provisions `ILog` from an `ILoggerFactory` layer input. All Axial log levels and
exception objects are preserved. The adapter never silently substitutes a no-op logger.

## Fiber Defect Logging

Install the observer once around the root application:

```fsharp
let observed (logger: Microsoft.Extensions.Logging.ILogger) (application: Flow<unit, string, unit>) =
    application
    |> FiberLogging.observe logger
```

Fiber defects are errors; unobserved fiber defects are critical entries. Compose it with telemetry when both are
required:

```fsharp no-check reason="Needs Axial.Telemetry as well as Axial.Hosting; this page checks against Axial.Hosting"
let observedWithSpans (logger: Microsoft.Extensions.Logging.ILogger) (application: Flow<unit, string, unit>) =
    application
    |> Flow.withFiberObserver
        (FiberObserver.compose
            FiberTelemetry.observerWithSpans
            (FiberLogging.observer logger))
```

Logging is an explicit application dependency; telemetry remains runtime instrumentation. See
[Observability](/observability/index.html).

## Desktop and Embedded Applications

Desktop frameworks already own application lifetime. Start `App` after startup and await stop from the framework's
closing path:

```fsharp
let startDesktop (requestExit: unit -> unit) (application: Flow<unit, string, unit>) =
    let running = App.start () application

    let closeApplication () = async {
        let! _ = running.Stop()
        requestExit ()
    }

    closeApplication
```

Do not block the UI thread on `Completion`. Framework-specific packages are unnecessary unless an integration can
provide more than wiring one close event to `Stop()`.

## Other Runtimes

- [Node hosting](./node.html) handles Node arguments, `process.env`, signals, and exit status.
- [Browser hosting](./browser.html) handles explicit UI ownership and `AbortSignal`.
