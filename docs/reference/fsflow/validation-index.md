---
title: index
---

Prefixes a validation with an indexed branch.


```fsharp
let index (index: int) (validation: Validation<'value, 'error>) : Validation<'value, 'error>
```




## Parameters

- `index`: The branch index.
- `validation`: The validation to scope.

## Returns

A validation whose diagnostics are prefixed with `Index index`.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L415)

