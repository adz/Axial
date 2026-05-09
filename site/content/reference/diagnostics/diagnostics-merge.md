---
title: merge
type: docs
---

Recursively merges two diagnostics graphs, combining shared branches and local errors.


```fsharp
let rec merge (left: Diagnostics<'error>) (right: Diagnostics<'error>) : Diagnostics<'error>
```


## Remarks

This is the core operation for applicative validation. It ensures that errors from sibling
fields are collected together into a single structured graph.


## Parameters

- `left`: The first graph of type `Diagnostics`.
- `right`: The second graph of type `Diagnostics`.

## Returns

A new `Diagnostics` containing the union of both inputs.

## Information

- **Module**: `Diagnostics`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L89)

