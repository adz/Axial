---
weight: 20
title: "Tutorial: Layers"
description: Building and providing an explicit environment with Layer.
---

# Tutorial: Layers

This tutorial builds a small explicit environment with a layer and runs a workflow through `Flow.provide`.

## Define Services

```fsharp
open System
open System.Threading.Tasks
open FsFlow

type IOrders =
    abstract Save: string -> Task<unit>

type AppEnv =
    { Orders: IOrders }

    interface IHas<IOrders> with
        member this.Service = this.Orders
```

## Write The Workflow

```fsharp
let saveOrder orderId : Flow<AppEnv, string, unit> =
    flow {
        let! orders = Service<IOrders>.get()
        do! orders.Save orderId
    }
```

## Build A Layer

```fsharp
let ordersLayer : Layer<unit, string, IOrders> =
    Layer.succeed
        { new IOrders with
            member _.Save orderId =
                task { printfn "saved %s" orderId } }

let appLayer : Layer<unit, string, AppEnv> =
    ordersLayer
    |> Layer.map (fun orders -> { Orders = orders })
```

## Run Through Flow.provide

```fsharp
let run () = task {
    let! exit =
        saveOrder "A-100"
        |> Flow.provide appLayer
        |> Flow.run ()

    match exit with
    | Exit.Success () -> printfn "done"
    | Exit.Failure cause -> printfn "failed: %A" cause
}
```

The workflow stays typed against `AppEnv`. The layer owns construction. If the layer later opens a disposable resource,
register it with the provided `Scope`.
