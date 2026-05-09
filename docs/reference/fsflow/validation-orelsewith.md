---
title: Validation.orElseWith
linkTitle: orElseWith
---

Computes a fallback validation from the source diagnostics when validation fails.


```fsharp
let orElseWith (fallback: Diagnostics<'error> -> Validation<'value, 'error>) (validation: Validation<'value, 'error>) : Validation<'value, 'error>
```


## Remarks

This is the lazy counterpart to `orElse` and is useful when the alternate
branch depends on the accumulated diagnostics.


## Parameters

- `fallback`: A function that turns the diagnostics into an alternate validation.
- `validation`: The source validation.

## Returns

The source validation when it succeeds, otherwise the computed fallback validation.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L339)

