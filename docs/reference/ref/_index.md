---
title: "Ref"
weight: 110
---

This page shows the `Ref` surface for small pieces of shared mutable state inside flows. A `Ref<'T>` is an atomic handle that can be created, read, set, updated, or modified from workflow code without turning the whole environment into a mutable object. Use `Ref` for counters, flags, request-local caches, and coordination points where a single value is enough. For multi-value invariants that must change together, use STM instead.

## Core type

- [`Ref`](./t-ref.md):

## Module functions

- [`Ref.make`](./m-ref-make.md): Creates a new <a href="t-ref.md">Ref</a> with the initial value.
- [`Ref.get`](./m-ref-get.md): Reads the current value of the reference.
- [`Ref.set`](./m-ref-set.md): Sets the value of the reference to the specified value.
- [`Ref.update`](./m-ref-update.md): Updates the value of the reference using the supplied function.
- [`Ref.modify`](./m-ref-modify.md): Updates the value of the reference using the supplied function and returns a derived value.
