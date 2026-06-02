---
weight: 50
title: Service Provider Boundaries
description: Keeping IServiceProvider at the host edge.
---

# Service Provider Boundaries

`IServiceProvider` is useful host infrastructure, but it is not the main FsFlow application model. Keep provider lookup
at the edge and move into explicit environments before core workflows.

## Direct Resolve

Use `Service<'service>.resolve()` when dynamic lookup is the intended boundary behavior.

```fsharp
let handler : Flow<IServiceProvider, unit, unit> =
    flow {
        let! orders = Service<IOrderRepository>.resolve()
        do! orders.Flush()
    }
```

Missing registrations become defects because they are configuration bugs.

## Provider-Backed Layers

Use layers when startup should validate requirements before the core workflow runs.

```fsharp
open System.Threading.Tasks

let ordersLayer : Layer<IServiceProvider, StartupError, IOrderRepository> =
    Layer.effect (fun (provider, _) _ ->
        match provider.GetService(typeof<IOrderRepository>) with
        | null -> ValueTask(Exit.Failure (Cause.Fail MissingOrders))
        | service -> ValueTask(Exit.Success (service :?> IOrderRepository)))
```

Then compose provider-backed layers into the application environment and run the real workflow with `Flow.provide`.

## Boundary Rule

Use `IServiceProvider` to build the world. Do not make every business workflow depend on it.
