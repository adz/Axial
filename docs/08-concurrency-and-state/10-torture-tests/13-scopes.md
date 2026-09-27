---
title: Scopes
description: "A random tree of nested scopes and forked children acquires every kind of resource, then fails, dies, or is interrupted."
---

# Scopes

Each round builds a random tree of [scopes](/scopes/index.html). Every scope acquires a few resources, opens child
scopes, and forks children, then may fail or die, and the whole tree may be interrupted part way.

Run it with `dotnet run --project examples/Axial.TortureTest -- scopes 100`.

## What it does

- Each resource is acquired in one of the ways Axial offers: `Flow.scopeResource` with `Resource.ofAsync`,
  `Resource.create`, `Resource.finalizer`, or `Resource.asyncFinalizer`; `Flow.scopeAsyncFinalizer`,
  `scopeFinalizer`, `scopeAcquireRelease`, `scopeDisposable`, `scopeAsyncDisposable`; or `Scope.AddFinalizer`. Each
  logs when it is acquired and released.
- A scope's children are nested with `Flow.scoped`, forked and joined, forked and interrupted, or forked and left for
  the scope to interrupt when it closes.
- Every scope counts itself in, and counts itself out with `Flow.ensuring` and `Flow.onInterrupt`.

{{< snippet id="torture-scopes" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| Every acquired resource was released exactly once | However a scope ends, it releases what was acquired in it, once. |
| Each scope released its resources in reverse order | Finalizers run in the reverse of their registration order. |
| Every resource was released after it was acquired | Release never runs early. |
| Every scope that started ran its ensuring finalizer exactly once | `Flow.ensuring` runs on success, failure, defect, and interruption. |
| No more scopes were interrupted than started | `Flow.onInterrupt` runs only for interrupted scopes. |
