---
title: Validation.map
linkTitle: map
---

Maps the successful value of a validation.


```fsharp
let map (mapper: 'value -> 'next) (validation: Validation<'value, 'error>) : Validation<'next, 'error>
```




## Parameters

- `mapper`: A function of type `'value -> 'next`.
- `validation`: The source `Validation`.

## Returns

A validation with the transformed success value.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L218)

