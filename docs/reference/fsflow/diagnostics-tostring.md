---
title: Diagnostics.toString
linkTitle: toString
---

Renders a diagnostics graph in a YAML-like layout for display.


```fsharp
let toString (graph: Diagnostics<'error>) : string
```


## Remarks

This is intended for human-readable output. Empty sections are omitted, and children are
shown directly under their branch labels at the same indentation level as errors. Errors
render as YAML-style bullet items without an `Errors:` key. Use
`flatten` when you need path-bearing diagnostics for
reporting or assertions.


## Parameters

- `graph`: The diagnostics graph to render.

## Returns

A formatted string representation of the graph.

## Information

- **Module**: `Diagnostics`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L110)

