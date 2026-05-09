---
title: Validation.fromResult
linkTitle: fromResult
---

Lifts a standard `Result` into the `Validation` context.


```fsharp
let fromResult (result: Result<'value, 'error>) : Validation<'value, 'error>
```


## Remarks

If the result is an error, it is wrapped in a root-level `Diagnostics` graph.


## Parameters

- `result`: The result to lift.

## Returns

A `Validation` mirroring the result.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L209)

