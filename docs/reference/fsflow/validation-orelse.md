---
title: orElse
---

Falls back to another validation when the source validation fails.


```fsharp
let orElse (fallback: Validation<'value, 'error>) (validation: Validation<'value, 'error>) : Validation<'value, 'error>
```


## Remarks

This is a left-biased choice operator. If the source succeeds, the fallback is not used.
If the source fails, the fallback validation is returned as-is.


## Parameters

- `fallback`: The validation to use when the source fails.
- `validation`: The source validation.

## Returns

The source validation when it succeeds, otherwise the fallback validation.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L323)

