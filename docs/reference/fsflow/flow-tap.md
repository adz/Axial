---
title: tap
---

Runs a synchronous side effect on success and preserves the original value.


```fsharp
let tap (binder: 'value -> Flow<'env, 'error, unit>) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value>
```


## Remarks

Use this for logging, telemetry, or other "fire and forget" operations that should not
alter the primary value path. If the `binder` flow fails, the entire
flow fails with that error.


## Parameters

- `binder`: A function that produces a side-effect flow from the successful value.
- `flow`: The source flow.

## Returns

A `Flow` that preserves the original success value after the side effect.

## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L185)

