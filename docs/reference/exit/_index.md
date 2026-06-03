---
title: "Exit"
weight: 30
---

This page shows the `Exit<'value, 'error>` type, which represents the final outcome of an FsFlow execution. Every flow eventually resolves to either a success or a failure cause. Use the `Exit` module functions to transform outcomes without manually pattern matching at every boundary.

## Core type

- [`Exit`](./t-exit.md):
 Represents the final outcome of a workflow execution.


## Module functions

- [`Exit.map`](./m-exit-map.md): Transforms the success value of an exit outcome using the provided function.
- [`Exit.bind`](./m-exit-bind.md): Binds the success value of an exit outcome to a function that returns a new exit outcome.
- [`Exit.mapError`](./m-exit-maperror.md): Transforms the error value of a failed exit outcome using the provided function.
- [`Exit.mapBoth`](./m-exit-mapboth.md): Transforms both success and failure outcomes of an exit using the provided functions.
- [`Exit.fromResult`](./m-exit-fromresult.md): Creates an exit outcome from a standard F# <code>Result</code>.
- [`Exit.toResult`](./m-exit-toresult.md): Converts an exit outcome to a standard F# <code>Result</code>.
