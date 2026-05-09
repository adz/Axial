---
title: toResult
type: docs
---

Converts a `Validation` into a standard `Result`.


```fsharp
let toResult (validation: Validation<'value, 'error>) : Result<'value, Diagnostics<'error>>
```




## Parameters

- `validation`: The validation to convert.

## Returns

A result containing either the success value or the full diagnostics graph.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L176)

