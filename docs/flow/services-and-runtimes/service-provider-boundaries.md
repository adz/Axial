---
weight: 60
title: Service Provider Boundaries
description: Keeping IServiceProvider at the host edge.
---

# Service Provider Boundaries

`IServiceProvider` is useful host infrastructure, but it is not the main Axial application model. Keep provider lookup
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
    Layer.fromValueTask (fun (provider, _) _ ->
        match provider.GetService(typeof<IOrderRepository>) with
        | null -> ValueTask(Exit.Failure (Cause.Fail MissingOrders))
        | service -> ValueTask(Exit.Success (service :?> IOrderRepository)))
```

Then compose provider-backed layers into the application environment and run the real workflow with `Flow.provide`.

`Axial.Flow.PlatformService` follows this pattern with `BaseRuntime.fromServiceProvider`. Register `IClock`, `ILog`,
`IRandom`, `IGuid`, and `IEnvironmentVariables` in a Microsoft DI `ServiceCollection`, build the provider at the host
edge, and use the layer to convert those dynamic registrations into an explicit `BaseRuntime`. Missing registrations
fail as typed startup errors through `BaseRuntimeError.MissingService`, while direct `Service<'T>.resolve()` remains a
defect-oriented escape hatch for host-edge code.

## Boundary Rule

Use `IServiceProvider` to build the world. Do not make every business workflow depend on it.
