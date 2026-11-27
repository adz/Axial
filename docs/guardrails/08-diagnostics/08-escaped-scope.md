---
title: "escaped-scope (AXG008): use scoped services inside their run"
linkTitle: escaped-scope
description: Reports a scoped queue, hub, subscription or cache returned from the run that shuts it down.
---

# escaped-scope (AXG008)

## The problem

`Flow.run`, `RunSynchronously` and `StartAsTask` close their scope when the flow ends. A scoped queue
or hub registered with that scope shuts down then. Return one from the run and the caller gets a service that is
already shut down:

```fsharp
/// Reported: the queue shuts down when the run returns it.
let escapedQueue () : Exit<Queue<int>, Never> =
    Queue.makeScoped (QueueStrategy.BackPressure 16) |> Flow.run ()

/// Fixed: make and use the queue inside one flow.
let usedInside () : Exit<int, Never> =
    flow {
        let! queue = Queue.makeScoped (QueueStrategy.BackPressure 16)
        let! _ = Queue.offer 1 queue
        return! Dequeue.take queue
    }
    |> Flow.run ()
```

A cache captures the runtime that made it. Closing that runtime's scope interrupts its lookups, so the same applies.

## What it reports

A run that returns `Queue.makeScoped`, `Hub.makeScoped`, `Hub.subscribe`, or a cache from `Cache.make`,
`makeBounded`, `makeBoundedWithExpiry` or `makeRec`.

`Queue.make`, its `bounded`, `unbounded`, `dropping` and `sliding` forms, and `Hub.make` are not tied to a scope and
may be returned.

The check sees direct calls and pipelines that end in a scoped constructor. It also checks result types, which
catches cache flows built from parts, but not queues or hubs, whose type does not say whether they are scoped.

## When it is deliberate

When the caller keeps the owning scope open, use `// axial-allow-escaped-scope: <reason>`.
