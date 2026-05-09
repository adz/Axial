---
title: map3
---

Combines three validations, accumulating errors when any input fails.


```fsharp
let map3 (mapper: 'left -> 'middle -> 'right -> 'value) (left: Validation<'left, 'error>) (middle: Validation<'middle, 'error>) (right: Validation<'right, 'error>) : Validation<'value, 'error>
```




## Parameters

- `mapper`: A function of type `'left -> 'middle -> 'right -> 'value`.
- `left`: The first validation.
- `middle`: The second validation.
- `right`: The third validation.

## Returns

A validation with the combined result.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L305)

