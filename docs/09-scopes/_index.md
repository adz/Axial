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

<div class="scope-timeline" role="img" aria-label="Timeline showing Flow executions separately from root, lexical, child, and nested resource scopes">
<div class="scope-timeline-legend"><span><i class="scope-key scope-key--flow"></i>Flow execution</span><span><i class="scope-key scope-key--scope"></i>runtime scope</span><span><i class="scope-key scope-key--lexical"></i>lexical resource</span></div>
<div class="scope-timeline-axis"><span>execution starts</span><span>time →</span><span>execution returns</span></div>
<div class="scope-timeline-row"><span class="scope-timeline-label">root scope</span><span class="scope-timeline-track"><span class="scope-timeline-bar scope-timeline-bar--root" style="--start: 0; --length: 100">application ownership</span></span></div>
<div class="scope-timeline-row"><span class="scope-timeline-label">Flow A</span><span class="scope-timeline-track"><span class="scope-timeline-bar scope-timeline-bar--flow" style="--start: 8; --length: 32">ordinary Flow</span></span></div>
<div class="scope-timeline-row"><span class="scope-timeline-label"><code>use</code> / <code>use!</code> in Flow A</span><span class="scope-timeline-track"><span class="scope-timeline-bar scope-timeline-bar--lexical" style="--start: 15; --length: 23">reader</span></span></div>
<div class="scope-timeline-row"><span class="scope-timeline-label">Flow B</span><span class="scope-timeline-track"><span class="scope-timeline-bar scope-timeline-bar--flow" style="--start: 45; --length: 48">ordinary Flow</span></span></div>
<div class="scope-timeline-row"><span class="scope-timeline-label"><code>Flow.scoped</code> in B</span><span class="scope-timeline-track"><span class="scope-timeline-bar scope-timeline-bar--child" style="--start: 52; --length: 36">child ownership</span></span></div>
<div class="scope-timeline-row"><span class="scope-timeline-label">Flow C inside child</span><span class="scope-timeline-track"><span class="scope-timeline-bar scope-timeline-bar--flow" style="--start: 59; --length: 22">shares child</span></span></div>
<div class="scope-timeline-row"><span class="scope-timeline-label">nested <code>Flow.scoped</code></span><span class="scope-timeline-track"><span class="scope-timeline-bar scope-timeline-bar--nested" style="--start: 65; --length: 13">nested</span></span></div>
</div>

The Flow bars show when code runs. They do not create ownership boundaries: Flow A and Flow B both use the root scope.
The scope bars show when registered resources and fibers are owned. Flow C runs inside the child scope, so its
registrations belong to that child. `use` and `use!` are narrower still and dispose when their lexical body exits.
`Flow.scoped` creates a child scope and closes it before returning; a nested scope closes before its parent. The outer
execution boundary—`Flow.run` or another host runner—owns the root scope.

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
