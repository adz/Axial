---
weight: 20
title: Layers
description: Provisioning explicit environments with Layer and Flow.provide.
type: docs
---


A `Layer<'input, 'error, 'output>` builds an environment or service bundle from an input value. It runs inside a
`Scope`, so resources acquired during provisioning can be finalized when the provided flow finishes.

```fsharp
let appFlow : Flow<AppEnv, AppError, unit> =
    placeOrder order

let runnable : Flow<IServiceProvider, AppError, unit> =
    appFlow |> Flow.provide appLayer
```

## Primary Shape

Use `layer { }` for application environment construction:

```fsharp
let appLayer =
    layer {
        let! runtime = BaseRuntime.fromServiceProvider
        and! orders = ordersLayer

        return { Runtime = runtime; Orders = orders }
    }
```

Plain `let!` is sequential and dependent. Sibling `and!` bindings are independent and use `Layer.merge`, which provisions
branches in parallel through child scopes.

## Layer Surface

The core layer surface is:

```fsharp
Layer.succeed value
Layer.read projection
Layer.effect provision
Layer.map mapper layer
Layer.mapError mapper layer
Layer.bind binder layer
Layer.zip left right
Layer.zipPar left right
Layer.merge left right
Layer.map2 mapper left right
Layer.map3 mapper left middle right
```

Use `Layer.succeed` for already-built values, `Layer.effect` when construction can fail or register cleanup, and
`Layer.bind` / `layer { let! }` when the next provisioning step depends on an earlier value.

## Example

```fsharp
open System.Threading.Tasks

type AppEnv =
    { Runtime: BaseRuntime
      Orders: IOrderRepository }

    interface IHas<IClock> with member this.Service = this.Runtime.Clock
    interface IHas<ILog> with member this.Service = this.Runtime.Log
    interface IHas<IOrderRepository> with member this.Service = this.Orders

let ordersLayer : Layer<IServiceProvider, BaseRuntimeError, IOrderRepository> =
    Layer.effect (fun (provider, _) _ ->
        match provider.GetService(typeof<IOrderRepository>) with
        | null ->
            ValueTask(Exit.Failure (Cause.Fail (BaseRuntimeError.MissingService "IOrderRepository")))
        | service ->
            ValueTask(Exit.Success (service :?> IOrderRepository)))

let appLayer : Layer<IServiceProvider, BaseRuntimeError, AppEnv> =
    layer {
        let! runtime = BaseRuntime.fromServiceProvider
        and! orders = ordersLayer

        return
        { Runtime = runtime
          Orders = orders }
    }
```

Layer error types must match the flow error type. When different provisioning steps use different errors, map them into
one startup error type before calling `Flow.provide`.

## let! And and!

Use `let!` when provisioning is dependent:

```fsharp
let ordersLayerFromConfig config : Layer<IServiceProvider, BaseRuntimeError, IOrderRepository> =
    Layer.effect (fun (provider, scope) cancellationToken ->
        // Build or resolve the repository from config, provider, and scope.
        provisionOrders config provider scope cancellationToken)

let appLayer =
    layer {
        let! config = configLayer
        let! orders = ordersLayerFromConfig config

        return { Orders = orders }
    }
```

Use sibling `and!` when provisioning is independent:

```fsharp
let appLayer =
    layer {
        let! runtime = BaseRuntime.fromServiceProvider
        and! orders = ordersLayer

        return { Runtime = runtime; Orders = orders }
    }
```

An `and!` sibling cannot depend on a value introduced by another sibling. That remains an ordinary F# compile-time
scope error, which is the desired signal: if a service needs another value, separate it into a prior `let!`.

## zip, zipPar, And merge

`Layer.zip` provisions left then right, sequentially. Use it when ordering is intentional.

`Layer.zipPar` provisions both sides independently in parallel and returns a tuple.

`Layer.merge` is the layer-domain name for `zipPar`. Prefer it when combining service bundles or environment fragments:

```fsharp
let combined =
    Layer.merge runtimeLayer ordersLayer
    |> Layer.map (fun (runtime, orders) -> { Runtime = runtime; Orders = orders })
```

`Layer.merge` does not automatically merge `IHas<'service>` contracts or synthesize a new environment type. It only
provisions both sides and returns their outputs. Keep the final environment explicit:

```fsharp
type AppEnv =
    { Runtime: BaseRuntime
      Orders: IOrderRepository }

    interface IHas<IClock> with member this.Service = this.Runtime.Clock
    interface IHas<IOrderRepository> with member this.Service = this.Orders
```

This keeps service requirements visible to people, the compiler, and LLMs. It also avoids ambiguous cases such as two
services with the same implementation type. If an application needs multiple instances of the same service shape, give
them named record fields or distinct nominal contracts rather than relying on tags.

`Layer.map2` and `Layer.map3` are sequential mapping helpers that avoid nested tuple reshaping. In a computation
expression, sibling `and!` bindings use `merge` instead.

## Cleanup

`Flow.provide` creates a root scope, builds the layer, runs the downstream flow, and closes the scope. Cleanup runs when
the layer fails, the downstream flow fails, or the downstream flow succeeds.

Use `Layer.acquireRelease` when a layer provisions a service implementation or resource that must live for the whole
provided flow:

```fsharp
let connectionLayer =
    Layer.acquireRelease
        (Layer.effect (fun (connectionString, _) _ ->
            openConnection connectionString
            |> EffectFlow.ofValue))
        (fun connection _ ->
            connection.Dispose()
            Task.CompletedTask)
```

Parallel layer composition uses parent-owned child scopes. If one branch fails after another branch acquired resources,
the acquired branch is finalized when the root scope closes. If both parallel branches fail, FsFlow returns the left
failure for now; richer parallel cause accumulation is deferred until the `Cause` model grows that shape.
