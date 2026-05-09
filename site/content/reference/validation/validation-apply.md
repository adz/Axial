---
title: apply
type: docs
---

Applies a validation-wrapped function to a validation-wrapped value.


```fsharp
let apply (validation: Validation<'value -> 'next, 'error>) (value: Validation<'value, 'error>) : Validation<'next, 'error>
```




## Parameters

- `validation`: The validation containing the function.
- `value`: The validation containing the value.

## Returns

The result of applying the function to the value, with accumulated errors.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L287)

