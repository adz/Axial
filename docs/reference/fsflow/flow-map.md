---
title: map
---

Maps the successful value of a synchronous flow.


```fsharp
let map (mapper: 'value -> 'next) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'next>
```


## Remarks

If the source `flow` fails, the `mapper` is not executed,
and the error is preserved. This allows for safe transformation of data within the flow.


## Parameters

- `mapper`: A function of type `'value -> 'next` to transform the successful value.
- `flow`: The source flow of type `Flow` to transform.

## Returns

A new `Flow` with the transformed success value of type `'next`.

## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L136)

