---
title: error
type: docs
---

Creates a failing validation result with the provided diagnostics.


```fsharp
let error (diagnostics: Diagnostics<'error>) : Validation<'value, 'error>
```




## Parameters

- `diagnostics`: The `Diagnostics` graph.

## Returns

A failing `Validation`.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L194)

