---
weight: 50
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

```fsharp
open System
open System.Threading
open System.Threading.Tasks
open Axial

type IOrders =
    abstract Save : string -> Task<unit>

type IClock =
    abstract UtcNow : unit -> DateTimeOffset

type AppEnv =
    { Orders: IOrders
      Clock: IClock }

let saveOrder (orderId: string) : Flow<AppEnv, string, unit> =
    flow {
        let! env = Flow.env
        do! env.Orders.Save orderId
        let now = env.Clock.UtcNow()
        printfn "[%O] saved %s" now orderId
    }
```

The workflow still depends on `AppEnv`. Layers only change how `AppEnv` gets built.

## 2. Build Small Layers

```fsharp
let ordersLayer : Layer<unit, string, IOrders> =
    Layer.succeed
        { new IOrders with
            member _.Save orderId =
                task {
                    // Imagine the real dependency here: open connection, transaction, etc.
                    printfn "persisting %s" orderId
                } }

let clockLayer : Layer<unit, string, IClock> =
    Layer.succeed
        { new IClock with
            member _.UtcNow() = DateTimeOffset.UtcNow }
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

If provisioning fails, `Flow.provide` never runs the downstream business workflow. That separation is one of the main reasons to use layers.

## 5. Resource Ownership

```fsharp
type FakeConnection() =
    interface IAsyncDisposable with
        member _.DisposeAsync() =
            ValueTask(Task.CompletedTask)

let connectionLayer : Layer<unit, string, FakeConnection> =
    Layer.acquireRelease
        (Layer.succeed (new FakeConnection()))
        (fun connection _ct -> connection.DisposeAsync().AsTask())
```

This is another main reason to use layers: acquired resources belong to the provisioning scope and are released when the provided workflow completes, fails, or is interrupted.

## 6. Run Through `Flow.provide`

```fsharp
let run () = task {
    let! exit =
        saveOrder "A-100"
        |> Flow.provide appLayer
        |> fun flow -> flow.ToTask(())

    match exit with
    | Exit.Success () -> printfn "done"
    | Exit.Failure cause -> printfn "failed: %A" cause
}
```

The call site stays small:

- construct or choose the layer
- provide it once
- run the workflow

That is much cleaner than manually opening and closing startup resources around every feature entry point.
