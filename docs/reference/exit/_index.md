---
title: "Exit"
weight: 30
---

This page shows the `Exit<'value, 'error>` type, which is Axial's name for `Result<'value, Cause<'error>>`. We name it `Exit` because it represents a completed workflow execution, not an ordinary domain result. Use the `Exit` module functions to transform completed outcomes without manually pattern matching at every boundary.

## Core type

- [`Flow.Exit`](./t-flow-exit.md):
 Represents the final outcome of a workflow execution.


## Module functions

- [`Flow.Exit.map`](./m-flow-exit-map.md): Transforms the success value of an exit outcome using the provided function.
- [`Flow.Exit.bind`](./m-flow-exit-bind.md): Binds the success value of an exit outcome to a function that returns a new exit outcome.
- [`Flow.Exit.mapError`](./m-flow-exit-maperror.md): Transforms the error value of a failed exit outcome using the provided function.
- [`Flow.Exit.mapBoth`](./m-flow-exit-mapboth.md): Transforms both success and failure outcomes of an exit using the provided functions.
- [`Flow.Exit.fromResult`](./m-flow-exit-fromresult.md): Creates an exit outcome from a standard F# <code>Result</code>.
- [`Flow.Exit.toResult`](./m-flow-exit-toresult.md): Converts an exit outcome to a standard F# <code>Result</code>.
