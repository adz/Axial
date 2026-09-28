---
title: "Tutorial: Layers"
description: Provision environments, manage resources, and keep startup concerns out of workflow code.
---

# Tutorial: Layers

Layers are for construction time, not business logic time.

Use a layer when you need to:

- build an environment from other services or config
- fail during provisioning before the workflow starts
- own resources that must be cleaned up exactly once
- compose several independent startup steps in parallel

## 1. The Workflow Still Targets An Environment

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
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers

type IOrders =
    abstract Save : string -> Task<unit>

type IClock =
    abstract UtcNow : unit -> DateTimeOffset

type AppEnv =
    { Orders: IOrders
      Clock: IClock }

let saveOrder (orderId: string) : Flow<AppEnv, string, string> =
    flow {
        let! env = Flow.env
        do!
            ColdTask(fun _ ->
                task {
                    do! env.Orders.Save orderId
                    return ()
                })
        let today = env.Clock.UtcNow().ToString "yyyy-MM-dd"
        return $"saved {orderId} at {today}"
    }
```

The workflow still depends on `AppEnv`. Layers only change how `AppEnv` gets built.

## 2. Build Small Layers

```fsharp
let persisted = ResizeArray<string>()

let ordersLayer : Layer<unit, string, IOrders> =
    Layer.succeed
        { new IOrders with
            member _.Save orderId =
                task {
                    // The real dependency goes here: open a connection, run a transaction.
                    persisted.Add orderId
                } }

let clockLayer : Layer<unit, string, IClock> =
    Layer.succeed
        { new IClock with
            member _.UtcNow() = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) }
```

## 3. Merge Them Into An App Layer

```fsharp
let appLayer : Layer<unit, string, AppEnv> =
    layer {
        let! orders = ordersLayer
        and! clock = clockLayer

        return
            { Orders = orders
              Clock = clock }
    }
```

Use plain `let!` when one provisioning step depends on another. Use sibling `and!` when the steps are independent.

## 4. Provision Failure Happens Before Business Logic

```fsharp
let failingOrdersLayer : Layer<unit, string, IOrders> =
    Layer.fromTask (fun _ _ ->
        task {
            return Exit.Failure (Cause.Fail "database connection string missing")
        })
```

```fsharp run
persisted.Clear()

let failingAppLayer : Layer<unit, string, AppEnv> =
    layer {
        let! orders = failingOrdersLayer
        and! clock = clockLayer
        return { Orders = orders; Clock = clock }
    }

saveOrder "A-100" |> Layer.provide failingAppLayer |> Flow.run () |> shouldEqual (Exit.Failure(Cause.Fail "database connection string missing"))
persisted.Count |> shouldEqual 0
```

If provisioning fails, `Layer.provide` never runs the downstream business workflow: nothing was persisted. That
separation is one of the main reasons to use layers.

## 5. Resource Ownership

```fsharp
type FakeConnection() =
    member val Disposed = false with get, set

    interface IAsyncDisposable with
        member this.DisposeAsync() =
            this.Disposed <- true
            ValueTask(Task.CompletedTask)

let connection = new FakeConnection()

let connectionLayer : Layer<unit, string, FakeConnection> =
    Layer.acquireRelease
        (Layer.succeed connection)
        (fun connection _ct -> (connection :> IAsyncDisposable).DisposeAsync().AsTask())
```

```fsharp run
Flow.envWith (fun (open': FakeConnection) -> open'.Disposed)
|> Layer.provide connectionLayer
|> Flow.run ()
|> shouldEqual (Exit.Success false)

connection.Disposed |> shouldEqual true
```

While the workflow ran, the connection was open; once `Layer.provide` finished, it was disposed.

Layers also own what they acquire: acquired resources belong to the provisioning scope and are released when the provided workflow completes, fails, or is interrupted.

## 6. Run Through `Layer.provide`

```fsharp run
persisted.Clear()

saveOrder "A-100"
|> Layer.provide appLayer
|> Flow.run ()
|> shouldEqual (Exit.Success "saved A-100 at 2026-01-01")

List.ofSeq persisted |> shouldEqual [ "A-100" ]
```

The call site stays small:

- construct or choose the layer
- provide it once
- run the workflow

Feature entry points no longer open and close startup resources themselves.
