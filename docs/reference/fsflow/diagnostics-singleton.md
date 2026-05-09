---
title: Diagnostics.singleton
linkTitle: singleton
---

Creates a diagnostics graph containing exactly one error at the root.


```fsharp
let singleton (error: 'error) : Diagnostics<'error>
```




## Parameters

- `error`: The application error to store at the root.

## Returns

A `Diagnostics` with a single error.

## Information

- **Module**: `Diagnostics`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L75)

