---
title: mapError
type: docs
---

Maps the error type of a validation graph.


```fsharp
let mapError (mapper: 'error -> 'nextError) (validation: Validation<'value, 'error>) : Validation<'value, 'nextError>
```




## Parameters

- `mapper`: A function of type `'error -> 'nextError`.
- `validation`: The source `Validation`.

## Returns

A validation with transformed error values.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L245)

