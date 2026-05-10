---
title: "FsFlow.Flow.tap"
linkTitle: "tap`"
---

Runs a synchronous side effect on success and preserves the original value.

## Remarks

Use this for logging, telemetry, or other "fire and forget" operations that should not
 alter the primary value path. If the `binder` flow fails, the entire
 flow fails with that error.


## Parameters

- `binder`: A function that produces a side-effect flow from the successful value.
- `flow`: The source flow.

## Returns

A `Flow` that preserves the original success value after the side effect.

