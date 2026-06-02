---
weight: 30
title: Scopes and Resources
description: Deterministic cleanup with Scope.
type: docs
---


`Scope` owns cleanup for resources acquired during provisioning or execution. It is not a dependency container. It only
registers finalizers and closes them in a predictable order.

The contract is:

- finalizers run in reverse registration order
- finalizers run at most once
- registering after closure fails
- cleanup failures are aggregated
- cleanup failures are defects, not typed domain errors

## Register Cleanup

Use `Layer.effect` when a layer acquires a resource that must be closed.

```fsharp
let connectionLayer : Layer<ConnectionString, DbError, IDbConnection> =
    Layer.effect (fun (connectionString, scope) _ ->
        task {
            let connection = openConnection connectionString
            scope.AddDisposable connection
            return Exit.Success connection
        }
        |> ValueTask)
```

For async resources, use `AddAsyncDisposable` or `AddFinalizer`.

```fsharp
scope.AddFinalizer(fun cancellationToken ->
    telemetry.FlushAsync(cancellationToken))
```

## Root Scope

The root scope is owned by `Flow.provide`. Most application code should not create a scope directly. Use
`Flow.Runtime.scope` only for advanced helpers that need to register cleanup while a flow is running.
