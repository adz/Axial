---
title: map2
---

Combines two validations, accumulating errors if both fail.


```fsharp
let map2 (mapper: 'left -> 'right -> 'value) (left: Validation<'left, 'error>) (right: Validation<'right, 'error>) : Validation<'value, 'error>
```


## Remarks

This is the core applicative operation. If both `left` and 
`right` fail, their diagnostics graphs are merged.


## Parameters

- `mapper`: A function of type `'left -> 'right -> 'value`.
- `left`: The first validation.
- `right`: The second validation.

## Returns

A validation with the combined result.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L270)

