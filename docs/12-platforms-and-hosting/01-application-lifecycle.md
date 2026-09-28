---
title: Application Lifecycle
description: Starting one root Flow application, coordinating stop, and waiting for scoped cleanup.
---

# Application Lifecycle

`App` runs one root `Flow` as an owned application. Use it when the workflow represents the lifetime of a CLI,
desktop process, browser mount, Node process, worker, or another application rather than one request or operation.

Application code remains an ordinary Flow value. Provision its environment before handing it to `App`:

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
type AppError =
    | ConfigurationError of string
    | OrderError of string

    static member describe(error: AppError) =
        match error with
        | ConfigurationError message
        | OrderError message -> message

type IOrderRepository =
    abstract ProcessPending: unit -> Result<unit, AppError>

type AppEnv =
    { Orders: IOrderRepository
      Log: ILog }

type StartupInputs = { PendingOrders: int }

let program : Flow<AppEnv, AppError, unit> =
    flow {
        let! orders = Flow.envWith _.Orders
        return! orders.ProcessPending()
    }

let appLayer : Layer<StartupInputs, AppError, AppEnv> =
    Layer.envWith (fun inputs ->
        { Orders =
            { new IOrderRepository with
                member _.ProcessPending() =
                    if inputs.PendingOrders >= 0 then Ok() else Error(OrderError "negative backlog") }
          Log = Log.live })

let root : Flow<StartupInputs, AppError, unit> =
    program
    |> Layer.provide appLayer
```

`root` is the complete application description: startup inputs in, typed application failures out, and all resources
acquired by `Live.appLayer` scoped to the root execution.

## Run a Finite Application

Use `App.run` when the caller only needs the final outcome:

```fsharp
let run inputs = async {
    let! exit = App.run inputs root

    match exit with
    | Exit.Success () -> return 0
    | Exit.Failure cause ->
        eprintfn "%s" (Cause.prettyPrint AppError.describe cause)
        return 1
}
```

```fsharp run
run { PendingOrders = 3 } |> Async.RunSynchronously |> shouldEqual 0
run { PendingOrders = -1 } |> Async.RunSynchronously |> shouldEqual 1
```

`App.run` uses the caller's F# async cancellation token. It waits until the root scope closes, so layer and Flow
finalizers have finished when the returned `Exit` becomes available.

## Own a Long-Running Application

Use `App.start` when another module controls when the application stops:

```fsharp
let service : Flow<StartupInputs, AppError, unit> = Flow.never
```

```fsharp run
let running = App.start { PendingOrders = 0 } service
running.Status |> shouldEqual AppStatus.Running

// Called later by a signal handler, window close event, or UI unmount:
let finalExit = running.Stop() |> Async.RunSynchronously

(match finalExit with Exit.Failure cause -> Cause.isInterrupted cause | _ -> false) |> shouldEqual true
running.Status |> shouldEqual AppStatus.Completed
```

An `AppHandle<'error,'value>` exposes:

- `Status`: `Running`, `Stopping`, or `Completed`.
- `Completion`: the one final `Exit`, available to any number of observers.
- `Stop()`: requests cooperative interruption and waits for cleanup.

Calling `Stop()` several times is safe. Every caller observes the same final exit. Disposing the handle requests stop
but cannot await asynchronous finalizers; application shutdown code should await `Stop()` or `Completion`.

## External Cancellation

Use `App.startWithCancellation` or `App.runWithCancellation` when an existing owner already supplies a
`CancellationToken`:

```fsharp
let startWithHost (hostStopping: CancellationToken) =
    App.startWithCancellation hostStopping { PendingOrders = 0 } service
```

Cancellation is administrative interruption. It becomes `Cause.Interrupt`; it is not mapped into the application's
typed error channel.

## App and Direct Flow Execution

`Flow.run`, `Flow.startTask`, and `Flow.toAsync` remain the direct execution interface for individual workflows and
interop boundaries. `App` adds ownership around a root workflow:

| Use | Entry point |
| --- | --- |
| Execute one operation | `workflow |> Flow.run env` or `workflow |> Flow.startTask env` |
| Run a finite root application | `App.run env application` |
| Start and later stop a root application | `App.start env application` |
| Integrate with .NET Generic Host | [`Axial.Hosting`](./dotnet.html) |
| Run under Node signals | [Node hosting](./node.html) |
| Tie lifetime to a browser owner | [Browser hosting](./browser.html) |

`App` does not render errors, choose process exit codes, or subscribe to platform lifecycle events. Those decisions
belong to the application edge or one of the platform hosting packages.
