---
title: Scopes and Resources
description: Choose lexical or runtime-owned resource lifetimes.
---

# Scopes and Resources

.NET already has resources and F# already has `use` and `use!`. Axial's `flow { }` computation expression supports
`use` directly:

```fsharp
let readFirstLine path =
    flow {
        use reader = File.OpenText path
        return! ColdTask(fun _ -> reader.ReadLineAsync())
    }
```

This is a **lexical lifetime**. The compiler disposes `reader` when control leaves the body governed by that `use`,
including success and exceptional exit. In a `flow { }`, that means disposal happens before the flow produced by that
body completes. The resource cannot safely be returned for another function or a later workflow to use.

A Flow scope provides a wider runtime lifetime. Code can acquire a resource in one subflow, use it from other functions
or child fibers, and release it when the chosen Flow boundary closes. `Flow.scoped` creates that boundary:

```fsharp no-check reason="Application-specific connection operations are described in the surrounding prose"
Flow.scoped (
    flow {
        let! connection =
            Flow.scopeAcquireRelease
                openConnection
                closeConnection

        return! runApplicationWork connection
    })
```

Everything registered inside `Flow.scoped` is released before the resulting flow returns. Cleanup runs after success,
typed failure, defect, or interruption.

## Registering with the current scope

The `scope` prefix means "attach this value or cleanup action to the current Flow scope":

```fsharp no-check reason="Application-specific resources are described in the surrounding prose"
flow {
    do! Flow.scopeDisposable stream
    do! Flow.scopeAsyncDisposable response
    do! Flow.scopeFinalizer flushTelemetry
    do! Flow.scopeAsyncFinalizer saveState
}
```

Use `Flow.scopeDisposable` and `Flow.scopeAsyncDisposable` for resources that have already been created. Use
`Flow.scopeFinalizer` or `Flow.scopeAsyncFinalizer` for custom cleanup.

`Flow.scopeAcquireRelease` combines acquisition and registration without leaving a cancellation point between them:

```fsharp no-check reason="Application-specific cache type is described in the surrounding prose"
let acquireRequestCache =
    Flow.scopeAcquireRelease
        (Flow.succeed (new RequestCache()))
        (fun cache _ ->
            cache.Dispose()
            Task.CompletedTask)
```

The returned value remains available to later subflows. Its release runs when the current scope closes.

## Reusable resource descriptions

`Resource` separates a reusable acquisition description from the decision to acquire it in a particular scope:

```fsharp no-check reason="Application-specific connection operations are described in the surrounding prose"
let connectionResource =
    Resource.create openConnection closeConnection

let program =
    Flow.scoped (
        flow {
            let! connection =
                connectionResource
                |> Flow.scopeResource

            return! query connection
        })
```

Use `Resource.finalizer` for a reusable task-based cleanup description and `Resource.asyncFinalizer` for F# async
cleanup. `Flow.scopeResource` acquires the description, registers its release with the current scope, and returns its
value.

## Choosing a lifetime

Use `use` or `use!` when one computation-expression body exclusively owns the resource and the value does not escape.

Use `Flow.scoped` plus the `Flow.scope...` operations when ownership spans functions, subflows, fibers, or a composed
stream operation. Scope finalizers run in reverse registration order, at most once. Cleanup failures are defects and are
combined with any failure that caused the scope to close.

Layers use the same model. `Layer.acquireRelease` keeps a provisioned service alive until `Layer.provide` finishes.

[FlowStream](/streams/index.html) uses child scopes internally. Terminal consumption closes the stream's resources and
child fibers on completion, failure, interruption, or early termination. The stream guide covers the operator-level
lifetime rules.
