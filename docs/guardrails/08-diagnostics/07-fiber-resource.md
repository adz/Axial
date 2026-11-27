---
title: "fiber-resource (AXG007): keep a fiber's resource alive"
linkTitle: fiber-resource
description: Reports a forked flow that returns a resource it acquired, which the fiber releases before Fiber.join returns it.
---

# fiber-resource (AXG007)

## The problem

A forked fiber owns the [scoped resources](/scopes/index.html#scoped-resources) it acquires, and releases them when it
ends. `Fiber.join` returns the value after that. So a fiber that returns its resource hands back something already
closed:

```fsharp
let connection : Resource<unit, Never, MemoryStream> =
    Resource.create
        (Flow.ok (new MemoryStream()))
        (fun stream -> Flow.delay (fun () -> stream.Dispose(); Flow.ok ()))

/// Reported: the connection is released before join returns it.
let releasedBeforeUse =
    flow {
        let! fiber = Flow.scopeResource connection |> Flow.fork
        return! Fiber.join fiber
    }
```

It compiles, and reads like ZIO, where a forked fiber shares its parent's scope. In Axial the first use after the
join throws `ObjectDisposedException`.

`Flow.forkResource` acquires in a fiber but keeps the resource in the caller's scope:

```fsharp
/// Fixed: the caller's scope owns the connection.
let keptForUse =
    flow {
        let! connecting = Flow.forkResource connection
        return! Fiber.join connecting
    }
```

To acquire several at once, use `Flow.zipPar` or `Flow.sequencePar`.
[Concurrency and scoped resources](/scopes/index.html#concurrency-and-scoped-resources) shows which scope owns a
resource in each combinator.

## What it reports

`Flow.fork`, `forkNamed`, `forkGraceful`, `forkDetached`, `forkReplacing` or `forkReplacingKey` of a
`Flow.scopeResource` or `Flow.scopeAcquireRelease` call, or of a `flow { }` that returns what it acquired with one.

## When it is deliberate

If the fiber owns and uses the resource itself, and returns it only for inspection after release, use
`// axial-allow-fiber-resource: <reason>`.
