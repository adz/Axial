---
title: merge
type: docs
---

Merges two validations into a validation of a tuple.


```fsharp
let merge (left: Validation<'value, 'error>) (right: Validation<'next, 'error>) : Validation<'value * 'next, 'error>
```




## Parameters

- `left`: The first validation.
- `right`: The second validation.

## Returns

A validation containing a tuple of the results.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L385)

