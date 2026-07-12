---
weight: 20
title: Managing Dependencies
description: How Axial models records, services, layers, scopes, and host-provider boundaries.
---

# Managing Dependencies

Keep dependencies explicit. Axial has one dependency model for v1: workflows read an explicit environment, reusable
helpers name service contracts, and layers build the environment at the boundary.

Use this order:

1. Use records plus `Flow.read` for most application code.
2. Use `IHas<'service>` plus `Service<'service>.get()` for reusable named services.
3. Use `Layer` and `Flow.provide` to build environments and own resource cleanup.
4. Use `Service<'service>.resolve()` only at .NET host edges where direct `IServiceProvider` lookup is intentional.

## Default Shape

Plain F# records are the default recommendation because they are legible, easy to fake in tests, and easy to refactor.

```fsharp
type ApiDeps = { Orders: IOrderRepo; Email: IEmailSender }

let workflow : Flow<ApiDeps, string, unit> =
    flow {
        let! email = Flow.read _.Email
        do! email.SendConfirmation()
    }
```

Keep the boundary concrete unless a named abstraction clearly pays for itself.

## Service Contracts

Reusable helpers can ask for a named service without forcing every application to use the same record shape:

```fsharp
type IHasOrders =
    inherit IHas<IOrderRepo>

let save order : Flow<#IHasOrders, OrderError, unit> =
    flow {
        let! orders = Service<IOrderRepo>.get()
        do! orders.Save order
    }
```

## Layers

Layers build explicit environments and own cleanup through `Scope`. Use `layer { }` when application startup needs to
combine several services into one environment.

```fsharp
let appLayer =
    layer {
        let! runtime = BaseRuntime.live
        and! orders = ordersLayer

        return { Runtime = runtime; Orders = orders }
    }
```

Use layers when construction can fail, when resources need cleanup, or when a host container should be validated once at
startup. Plain `let!` is sequential and dependent; sibling `and!` bindings are independent and use `Layer.merge`.

## Tutorials

For concrete starting points, use [App Record](../tutorials/app-record/) and [Layers](../tutorials/layers/).

## More Detail

- [Explicit Services](./explicit-services/)
- [Layers](./layers/)
- [Scopes and Resources](./scopes-and-resources/)
- [Building a Base Runtime](./building-a-base-runtime/)
- [Service Provider Boundaries](./service-provider-boundaries/)
