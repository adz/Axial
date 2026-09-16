---
title: Scopes
description: Choose lexical or runtime-owned resource lifetimes.
---

# Scopes

.NET already has resources, and F# already has `use` and `use!`. Axial's `flow { }` computation expression supports
both directly:

```fsharp
let readFirstLine path =
    flow {
        use reader = File.OpenText path
        return! ColdTask(fun _ -> reader.ReadLineAsync())
    }
```

This is a **lexical lifetime**. The compiler disposes `reader` when control leaves the body governed by that `use`. In a
`flow { }`, disposal happens before the Flow produced by that body completes. The resource cannot safely escape for
another function or later workflow to use.

A Flow scope is a runtime ownership boundary. It lets resources and child fibers live across function and subflow calls,
then closes all of them together after success, failure, interruption, or defect.

## Lifetime maps

The diagrams use containment literally: Flow B is a subflow inside Flow A, and a resource bar sits inside the boundary
that owns it.

### Lexical `use` ends with its subflow body

<div class="lifetime-case" role="img" aria-label="Flow A contains Flow B, whose lexical use resource ends when Flow B ends">
<div class="lifetime-axis"><span>Flow A starts</span><span>time →</span><span>Flow A ends</span></div>
<div class="lifetime-stage lifetime-stage--flow"><strong>Flow A</strong><span class="lifetime-region lifetime-region--subflow" style="--x: 18; --w: 38; --y: 2.4; --h: 4.8"><b>Flow B</b><span class="lifetime-region lifetime-region--lexical" style="--x: 14; --w: 72; --y: 1.8; --h: 2">use reader · dispose</span></span></div>
</div>

Flow B owns `reader` lexically. When Flow B returns to Flow A, `reader` has already been disposed.

### Registration in the current scope outlives Flow B

<div class="lifetime-case" role="img" aria-label="The root scope contains Flow A; Flow B registers a resource that remains owned after Flow B ends and is cleaned when the root scope closes">
<div class="lifetime-axis"><span>execution starts</span><span>time →</span><span>root scope closes</span></div>
<div class="lifetime-stage lifetime-stage--scope"><strong>current root scope</strong><span class="lifetime-region lifetime-region--flow" style="--x: 5; --w: 90; --y: 2.2; --h: 7"><b>Flow A</b><span class="lifetime-region lifetime-region--subflow" style="--x: 12; --w: 34; --y: 1.8; --h: 2.2">Flow B registers R</span><span class="lifetime-region lifetime-region--resource" style="--x: 30; --w: 64; --y: 4.5; --h: 1.7">R remains owned · cleanup</span></span></div>
</div>

Flow B ends, but `R` does not. `Flow.scopeResource` and the other `scope...` functions register with the current scope,
so `R` remains alive while Flow A continues and is cleaned only when that scope closes.

### `Flow.scoped` creates an earlier cleanup boundary

<div class="lifetime-case" role="img" aria-label="Flow A contains a child scope; Flow B registers a resource in it, Flow B ends, then the child scope cleans the resource before Flow A ends">
<div class="lifetime-axis"><span>Flow A starts</span><span>time →</span><span>Flow A ends</span></div>
<div class="lifetime-stage lifetime-stage--flow"><strong>Flow A</strong><span class="lifetime-region lifetime-region--scope" style="--x: 12; --w: 70; --y: 2.2; --h: 7"><b>Flow.scoped child</b><span class="lifetime-region lifetime-region--subflow" style="--x: 10; --w: 38; --y: 1.8; --h: 2.2">Flow B registers R</span><span class="lifetime-region lifetime-region--resource" style="--x: 27; --w: 66; --y: 4.5; --h: 1.7">R remains owned · cleanup</span></span></div>
</div>

Here Flow B still ends before `R` does, but the child scope closes before Flow A ends. This is the reason for
`Flow.scoped`: it chooses a runtime cleanup boundary independently of the function or subflow that acquired the
resource.

## Create a local runtime scope

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

Everything registered inside `Flow.scoped` remains available across calls made inside that block. Cleanup finishes
before the resulting Flow returns.

## Register with the current scope

The `scope` prefix means “attach this value or cleanup action to the current Flow scope”:

```fsharp no-check reason="Application-specific resources are described in the surrounding prose"
flow {
    do! Flow.scopeDisposable stream
    do! Flow.scopeAsyncDisposable response
    do! Flow.scopeFinalizer flushTelemetry
    do! Flow.scopeAsyncFinalizer saveState
}
```

Use `Flow.scopeDisposable` and `Flow.scopeAsyncDisposable` for resources that already exist. Use
`Flow.scopeFinalizer` or `Flow.scopeAsyncFinalizer` for custom cleanup.

`Flow.scopeAcquireRelease` combines acquisition and registration without an interruption point between them:

```fsharp no-check reason="Application-specific cache type is described in the surrounding prose"
let acquireRequestCache =
    Flow.scopeAcquireRelease
        (Flow.succeed (new RequestCache()))
        (fun cache _ ->
            cache.Dispose()
            Task.CompletedTask)
```

The returned value remains available to later subflows in the same scope.

## Reusable resource descriptions

`Resource` separates a reusable acquisition description from the scope that eventually owns it:

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

Use `Resource.finalizer` for task-based cleanup and `Resource.asyncFinalizer` for F# async cleanup.

## Fibers and nested work

A fiber created with `Flow.fork` belongs to the current scope. Closing the scope interrupts and awaits an unfinished
fiber. A nested `Flow.scoped` therefore gives a group of resources and fibers a lifetime shorter than the surrounding
application without requiring every function to pass cleanup handles manually.

Child scopes are also owned by their parent. If the root execution is interrupted, it closes every remaining child in
reverse registration order.

## Streams

[FlowStream](/streams/index.html) terminal consumers create child scopes internally. Stream resources and parallel
mapping fibers close on completion, failure, interruption, or early termination. The
[consuming streams](/streams/consuming.html) guide explains that boundary from the stream user's perspective.

## Rules to remember

- Use `use` or `use!` when one lexical body exclusively owns a disposable.
- Use `Flow.scoped` when ownership spans functions, subflows, fibers, or stream pulls.
- `scope...` functions attach cleanup to the current scope; they do not clean up immediately.
- Ordinary `Flow.bind` and `flow { }` nesting share the current scope.
- Nested scopes close before their parent.
- Finalizers run in reverse registration order and at most once.
- Cleanup failures are defects and combine with the failure that caused closure.

Layers use the same model. `Layer.acquireRelease` keeps a provisioned service alive until `Layer.provide` finishes.
