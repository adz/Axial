---
title: Diagnostics.flatten
linkTitle: flatten
---

Flattens the structured diagnostics graph into a linear list of diagnostics.


```fsharp
let flatten (graph: Diagnostics<'error>) : Diagnostic<'error> list
```


## Remarks

During flattening, child paths are accumulated from the root down into each emitted diagnostic.
The tree itself stores only local errors and child branches, while `Diagnostic`
is reserved for reporting output.


## Parameters

- `graph`: The `Diagnostics` to flatten.

## Returns

A list of type `Diagnostic` list.

## Information

- **Module**: `Diagnostics`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L150)

