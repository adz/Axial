---
title: "Ref"
weight: 110
type: docs
---

This page shows the `Ref` surface for small pieces of shared mutable state inside flows. A `Ref<'T>` is an atomic handle that can be created, read, set, updated, or modified from workflow code without turning the whole environment into a mutable object. Use `Ref` for counters, flags, request-local caches, and coordination points where a single value is enough. For multi-value invariants that must change together, use STM instead.

## Core type

- [`Flow.Ref`](./t-flow-ref.md):
 Represents a handle to a mutable reference that can be updated atomically.


## Module functions

- [`Flow.Ref.make`](./m-flow-ref-make.md): Creates a new <a href="https://learn.microsoft.com/dotnet/api/axial.ref-1">Ref</a> with the initial value.
- [`Flow.Ref.get`](./m-flow-ref-get.md): Reads the current value of the reference.
- [`Flow.Ref.set`](./m-flow-ref-set.md): Sets the value of the reference to the specified value.
- [`Flow.Ref.update`](./m-flow-ref-update.md): Updates the value of the reference using the supplied function.
- [`Flow.Ref.modify`](./m-flow-ref-modify.md): Updates the value of the reference using the supplied function and returns a derived value.
