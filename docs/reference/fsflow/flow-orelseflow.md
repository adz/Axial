---
title: orElseFlow
---

Turns a pure validation result into a synchronous flow with environment-provided failure.


```fsharp
let orElseFlow (errorFlow: Flow<'env, 'error, 'error>) (result: Result<'value, unit>) : Flow<'env, 'error, 'value>
```


## Remarks

This helper bridges the gap between pure validation (which often uses `Result` or `Check`)
and the `Flow` environment model. If the result is an error, the provided `errorFlow`
is executed to produce the final application error.


## Parameters

- `errorFlow`: A flow that reads the environment to produce an error value.
- `result`: The pure result to bridge.

## Returns

A `Flow` that mirrors the success of the result or fails with the outcome of the error flow.

## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L97)

