---
weight: 20
title: Layers
description: Provisioning explicit environments with Layer and Flow.provide.
---

# Layers

A `Layer<'input, 'error, 'output>` builds an environment or service bundle from an input value. It runs inside a
`Scope`, so resources acquired during provisioning can be finalized when the provided flow finishes.

```fsharp
let appFlow : Flow<AppEnv, AppError, unit> =
    placeOrder order

let runnable : Flow<IServiceProvider, AppError, unit> =
    appFlow |> Flow.provide appLayer
```

## Minimal Surface

The public layer surface is intentionally small:

```fsharp
Layer.succeed value
Layer.read projection
Layer.effect provision
Layer.map mapper layer
Layer.bind binder layer
Layer.zip left right
```

Use `Layer.succeed` for already-built values, `Layer.effect` when construction can fail or register cleanup, and
`Layer.zip` / `Layer.bind` to compose provisioning steps.

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
    Layer.zip
        BaseRuntime.fromServiceProvider
        ordersLayer
    |> Layer.map (fun (runtime, orders) ->
        { Runtime = runtime
          Orders = orders })
```

Layer error types must match the flow error type. When different provisioning steps use different errors, map them into
one startup error type before calling `Flow.provide`.

## Cleanup

`Flow.provide` creates a root scope, builds the layer, runs the downstream flow, and closes the scope. Cleanup runs when
the layer fails, the downstream flow fails, or the downstream flow succeeds.
