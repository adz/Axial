---
title: Choosing an Approach
description: How Axial models records, services, layers, scopes, and host-provider boundaries.
---

# Choosing an Approach

Axial has one dependency model: workflows read an explicit environment, reusable
helpers name service contracts, and layers build the environment at the boundary.

Use this order:

1. Use records plus `Flow.envWith` for most application code.
2. Declare a per-service contract for reusable named services.
3. Use `Layer` and `Layer.provide` to build environments and own resource cleanup.
4. Use `ServiceProvider.get` only at .NET host edges where direct `IServiceProvider` lookup is intentional.

## Default Shape

Plain F# records are the default recommendation because they are legible, easy to fake in tests, and easy to refactor.

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
type Order = { Id: int }
type OrderError = DuplicateOrder of int

type IOrderRepo =
    abstract Save: Order -> Result<unit, OrderError>

type IEmailSender =
    abstract SendConfirmation: Order -> unit

type ApiDeps = { Orders: IOrderRepo; Email: IEmailSender }

let placeOrder (order: Order) : Flow<ApiDeps, OrderError, unit> =
    flow {
        let! orders = Flow.envWith _.Orders
        let! email = Flow.envWith _.Email
        do! orders.Save order
        email.SendConfirmation order
    }
```

A test supplies the same record with fakes:

```fsharp
type MemoryOrders() =
    let saved = ResizeArray<int>()
    member _.Saved = List.ofSeq saved

    interface IOrderRepo with
        member _.Save order =
            if saved.Contains order.Id then
                Error(DuplicateOrder order.Id)
            else
                saved.Add order.Id
                Ok()

type MemoryEmail() =
    let sent = ResizeArray<int>()
    member _.Sent = List.ofSeq sent

    interface IEmailSender with
        member _.SendConfirmation order = sent.Add order.Id
```

```fsharp run
let orders = MemoryOrders()
let email = MemoryEmail()
let deps = { Orders = orders; Email = email }

placeOrder { Id = 1 } |> Flow.run deps |> shouldEqual (Exit.Success())
placeOrder { Id = 1 } |> Flow.run deps |> shouldEqual (Exit.Failure(Cause.Fail(DuplicateOrder 1)))
email.Sent |> shouldEqual [ 1 ]
```

Use a concrete record for the boundary. Add a named abstraction only when more than one implementation or caller needs it.

## Service Contracts

Reusable helpers can ask for a named service without forcing every application to use the same record shape:

```fsharp
type IHasOrders =
    abstract OrderRepo: IOrderRepo

let orderRepo<'env, 'error when 'env :> IHasOrders> : Flow<'env, 'error, IOrderRepo> =
    Flow.envWith _.OrderRepo

let save (order: Order) : Flow<#IHasOrders, OrderError, unit> =
    flow {
        let! orders = orderRepo
        do! orders.Save order
    }
```

Any environment that implements `IHasOrders` can run `save`:

```fsharp
type CheckoutEnv =
    { Orders: IOrderRepo }
    interface IHasOrders with
        member this.OrderRepo = this.Orders
```

```fsharp run
save { Id = 7 } |> Flow.run { Orders = MemoryOrders() } |> shouldEqual (Exit.Success())
```

## Layers

Layers build explicit environments and own cleanup through `Scope`. Use `layer { }` when application startup needs to
combine several services into one environment.

```fsharp
type AppEnv = { Runtime: BaseRuntime; Orders: IOrderRepo }

let ordersLayer : Layer<unit, OrderError, IOrderRepo> = Layer.succeed (MemoryOrders())

let appLayer : Layer<unit, OrderError, AppEnv> =
    layer {
        let! runtime = BaseRuntime.live |> Layer.widenError
        and! orders = ordersLayer

        return { Runtime = runtime; Orders = orders }
    }
```

`Layer.provide` builds the environment, runs the workflow in it, and releases whatever the layers acquired:

```fsharp run
let saveInApp : Flow<AppEnv, OrderError, unit> = save { Id = 3 } |> Flow.localEnv (fun app -> { Orders = app.Orders })

saveInApp |> Layer.provide appLayer |> Flow.run () |> shouldEqual (Exit.Success())
```

Use layers when construction can fail, when resources need cleanup, or when a host container should be validated once at
startup. Plain `let!` is sequential and dependent; sibling `and!` bindings are independent and use `Layer.merge`.

## Tutorials

For concrete starting points, use [App Record](tutorials/app-record.html) and [Layers](/layers/tutorial.html).

## More Detail

- [Service contracts](service-contracts.html)
- [Layers](/layers/layers.html)
- [Scopes and resources](/scopes/index.html)
- [Providing the environment](providing-the-environment.html)
