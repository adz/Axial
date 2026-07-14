---
weight: 5
title: Application Lifecycle
description: Starting one root Flow application, coordinating stop, and waiting for scoped cleanup.
---

# Application Lifecycle

`App` runs one root `Flow` as an owned application. Use it when the workflow represents the lifetime of a CLI,
desktop process, browser mount, Node process, worker, or another application rather than one request or operation.

Application code remains an ordinary Flow value. Provision its environment before handing it to `App`:

```fsharp
type AppEnv =
    { Orders: IOrderRepository
      Log: ILog }

type AppError =
    | ConfigurationError of string
    | OrderError of string

let program : Flow<AppEnv, AppError, unit> =
    flow {
        let! orders = Flow.read _.Orders
        return! orders.ProcessPending()
    }

let root : Flow<StartupInputs, AppError, unit> =
    program
    |> Flow.provide Live.appLayer
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

`App.run` uses the caller's F# async cancellation token. It waits until the root scope closes, so layer and Flow
finalizers have finished when the returned `Exit` becomes available.

## Own a Long-Running Application

Use `App.start` when another module controls when the application stops:

```fsharp
let running = App.start inputs root

printfn "State: %A" running.Status

// Called later by a signal handler, window close event, or UI unmount:
let stop = async {
    let! exit = running.Stop()
    printfn "Final exit: %A" exit
}
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
let running =
    App.startWithCancellation hostStopping inputs root
```

Cancellation is administrative interruption. It becomes `Cause.Interrupt`; it is not mapped into the application's
typed error channel.

## App and Direct Flow Execution

`ToTask`, `ToAsync`, and `RunSynchronously` remain the direct execution interface for individual workflows and
interop boundaries. `App` adds ownership around a root workflow:

| Use | Entry point |
| --- | --- |
| Execute one operation | `workflow.ToTask env` or `workflow.ToAsync env` |
| Run a finite root application | `App.run env application` |
| Start and later stop a root application | `App.start env application` |
| Integrate with .NET Generic Host | [`Axial.Flow.Hosting`](./hosting/) |
| Run under Node signals | [Node hosting](./hosting/node/) |
| Tie lifetime to a browser owner | [Browser hosting](./hosting/browser/) |

`App` does not render errors, choose process exit codes, or subscribe to platform lifecycle events. Those decisions
belong to the application edge or one of the platform hosting packages.
