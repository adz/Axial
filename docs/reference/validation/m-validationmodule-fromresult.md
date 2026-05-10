---
title: "FsFlow.ValidationModule.fromResult"
linkTitle: "fromResult`"
---

Lifts a standard `Result` into the `Validation` context.

## Remarks

If the result is an error, it is wrapped in a root-level `Diagnostics` graph.


## Parameters

- `result`: The result to lift.

## Returns

A `Validation` mirroring the result.

