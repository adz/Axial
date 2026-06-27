---
weight: 30
title: Scopes and Resources
description: Deterministic cleanup with Scope.
aliases:
  - /docs/managing-dependencies/scopes-and-resources/
---

# Scopes and Resources

`Scope` owns cleanup for resources acquired during provisioning or execution. It is not a dependency container. It only
registers finalizers and closes them in a predictable order.

This solves a different problem from `use` / `use!` in `flow { }`.

Use `use` / `use!` when the resource lifetime is local to one lexical block. Use scoped acquisition when a resource is
acquired in one effect, layer, subflow, or parallel branch and must remain alive until the surrounding runtime or layer
scope closes. That is the important scope problem: a service can be provisioned before the user flow starts, consumed by
many subflows, and released only when the whole `Flow.provide` boundary finishes.

The contract is:

- finalizers run in reverse registration order
- finalizers run at most once
- registering after closure fails
- cleanup failures are aggregated
- cleanup failures are defects, not typed domain errors
- child scopes are owned by their parent and close deterministically with it

## Local Acquire/Use/Release

Use `Flow.acquireReleaseWith` when acquisition, use, and release all belong to one flow expression.

```fsharp
let readFirstLine path =
    Flow.acquireReleaseWith
        (Flow.succeed (File.OpenText path))
        (fun reader _ ->
            reader.Dispose()
            Task.CompletedTask)
        (fun reader ->
            flow {
                return! reader.ReadLineAsync()
            })
```

This is the explicit combinator form of a local acquire/use/release block. The release action runs after the user flow
finishes, whether that flow succeeds, fails, defects, or is interrupted.

## Scoped Acquisition

Use `Flow.acquireRelease` when the acquired resource should live until the current runtime scope closes.

```fsharp
let acquireRequestCache =
    Flow.acquireRelease
        (Flow.succeed (new RequestCache()))
        (fun cache _ ->
            cache.Dispose()
            Task.CompletedTask)
```

The returned resource can be passed to later subflows. It is not released when the acquiring expression ends; it is
released when the surrounding execution scope or `Flow.provide` scope closes.

## Layer Resources

Use `Layer.acquireRelease` when a layer provisions a service implementation or resource that must be closed after the
provided flow finishes.

```fsharp
let connectionLayer : Layer<ConnectionString, DbError, IDbConnection> =
    Layer.acquireRelease
        (Layer.fromValueTask (fun (connectionString, _) _ ->
            openConnection connectionString
            |> Execution.ofValue))
        (fun connection _ ->
            connection.Dispose()
            Task.CompletedTask)
```

For lower-level cases, register finalizers directly through `Flow.addFinalizer`, `Layer.addFinalizer`, or `Scope`.

```fsharp
Flow.addFinalizer(fun cancellationToken ->
    telemetry.FlushAsync(cancellationToken))
```

## Root Scope

The root scope is owned by the execution boundary or `Flow.provide`. Most application code should not create a scope directly. Use
`Flow.acquireRelease`, `Layer.acquireRelease`, and the finalizer helpers first. Use `Flow.Runtime.scope` only for advanced
helpers that need direct access to the scope object.

## Child Scopes

`Scope.AddChild()` creates a parent-owned scope. Axial uses this internally for `Layer.zipPar` and `Layer.merge` so each
parallel provisioning branch can acquire resources independently.

If one parallel branch fails after another branch acquired resources, the successful branch cleanup still runs when
`Flow.provide` closes the root scope. Parent scopes close child scopes in a deterministic order, and each child still
applies its own reverse-registration finalizer order.
