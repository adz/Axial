---
title: ignore
---

Maps a successful validation value to `unit` while preserving the diagnostics.


```fsharp
let ignore (validation: Validation<'value, 'error>) : Validation<unit, 'error>
```




## Parameters

- `validation`: The source validation.

## Returns

A validation that keeps the original diagnostics and discards the success value.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L296)

