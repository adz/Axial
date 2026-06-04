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
    { Orders: IOrders
      ServiceName: string }

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

let nameLayer : Layer<unit, string, string> =
    Layer.succeed "orders"

let appLayer : Layer<unit, string, AppEnv> =
    layer {
        let! name = nameLayer
        and! orders = ordersLayer

        return
            { Orders = orders
              ServiceName = name }
    }
```

Plain `let!` is sequential. Sibling `and!` bindings are independent, so the layer builder uses `Layer.merge` and can
provision those branches in parallel.

## Run Through Flow.provide

```fsharp
let run () = task {
    let! exit =
        saveOrder "A-100"
        |> Flow.provide appLayer
        |> RunSynchronously or ToTask ()

    match exit with
    | Exit.Success () -> printfn "done"
    | Exit.Failure cause -> printfn "failed: %A" cause
}
```

The workflow stays typed against `AppEnv`. The layer owns construction. If the layer later opens a disposable resource,
register it with the provided `Scope`.
