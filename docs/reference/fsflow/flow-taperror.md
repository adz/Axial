---
title: tapError
---

Runs a synchronous side effect on failure and preserves the original error.


```fsharp
let tapError (binder: 'error -> Flow<'env, 'error, unit>) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value>
```


## Remarks

Use this for error logging or cleanup actions that depend on the environment.
If the `binder` side-effect flow itself fails, its error will
overwrite the original error.


## Parameters

- `binder`: A function that produces a side-effect flow from the error value.
- `flow`: The source flow.

## Returns

A `Flow` that preserves the original error after the side effect.

## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L204)

