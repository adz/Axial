---
title: "FsFlow.Flow.mapError"
linkTitle: "mapError`"
---

Maps the error value of a synchronous flow.

## Remarks

Transforms the error type of the flow while leaving successful values untouched.
 Useful for mapping internal errors into public-facing domain errors.


## Parameters

- `mapper`: The function to transform the error value.
- `flow`: The source flow.

## Returns

A `Flow` with the transformed error type.

