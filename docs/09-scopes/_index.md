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

## Lifetime map

<div class="scope-timeline" role="img" aria-label="Nested timeline showing lexical resources, subflows, child scopes, and nested scopes physically inside their owning Flow">
<div class="scope-timeline-legend"><span><i class="scope-key scope-key--flow"></i>Flow</span><span><i class="scope-key scope-key--subflow"></i>subflow</span><span><i class="scope-key scope-key--scope"></i>runtime scope</span><span><i class="scope-key scope-key--lexical"></i>lexical resource</span></div>
<div class="scope-timeline-axis"><span>execution starts</span><span>time →</span><span>execution returns</span></div>
<div class="scope-timeline-row"><span class="scope-timeline-label">root scope</span><span class="scope-timeline-track"><span class="scope-timeline-bar scope-timeline-bar--root" style="--start: 0; --length: 100">application ownership</span></span></div>
<div class="scope-timeline-row scope-timeline-row--flow"><span class="scope-timeline-label">Flow A</span><span class="scope-timeline-track scope-timeline-track--flow"><span class="scope-flow-frame" style="--start: 7; --length: 34"><strong>Flow A</strong><span class="scope-inner scope-inner--lexical" style="--inner-start: 20; --inner-length: 68"><code>use reader</code></span></span></span></div>
<div class="scope-timeline-row scope-timeline-row--flow scope-timeline-row--deep"><span class="scope-timeline-label">Flow B</span><span class="scope-timeline-track scope-timeline-track--flow"><span class="scope-flow-frame" style="--start: 44; --length: 50"><strong>Flow B</strong><span class="scope-inner scope-inner--child" style="--inner-start: 12; --inner-length: 78"><code>Flow.scoped</code><span class="scope-inner scope-inner--subflow" style="--inner-start: 10; --inner-length: 40">subflow</span><span class="scope-inner scope-inner--nested" style="--inner-start: 56; --inner-length: 34">nested scope</span></span></span></span></div>
</div>

Containment in the diagram is literal. The `use` resource is inside Flow A. Flow B contains a child scope; that child
contains both a subflow call and a nested scope. Calling or binding a subflow does not create another ownership scope:
it registers resources and fibers with whichever scope surrounds that call. The nested scope closes first, then its
parent child scope, and finally the root execution scope.

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
