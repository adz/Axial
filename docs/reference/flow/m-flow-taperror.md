---
title: "FsFlow.Flow.tapError"
linkTitle: "tapError`"
---

Runs a synchronous side effect on failure and preserves the original error.

## Remarks

Use this for error logging or cleanup actions that depend on the environment.
 If the `binder` side-effect flow itself fails, its error will
 overwrite the original error.


## Parameters

- `binder`: A function that produces a side-effect flow from the error value.
- `flow`: The source flow.

## Returns

A `Flow` that preserves the original error after the side effect.

