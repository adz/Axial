---
title: mapError
type: docs
---

Maps the error value of a synchronous flow.


```fsharp
let mapError (mapper: 'error -> 'nextError) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'nextError, 'value>
```


## Remarks

Transforms the error type of the flow while leaving successful values untouched.
Useful for mapping internal errors into public-facing domain errors.


## Parameters

- `mapper`: The function to transform the error value.
- `flow`: The source flow.

## Returns

A `Flow` with the transformed error type.

## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L224)

