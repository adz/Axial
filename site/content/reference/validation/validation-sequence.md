---
title: sequence
type: docs
---

Transforms a sequence of validations into a validation of a list.


```fsharp
let sequence (validations: seq<Validation<'value, 'error>>) : Validation<'value list, 'error>
```




## Parameters

- `validations`: The input sequence.

## Returns

A validation containing the list of values.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L378)

