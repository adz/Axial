---
title: "FsFlow.Flow.map"
linkTitle: "map`"
---

Maps the successful value of a synchronous flow.

## Remarks

If the source `flow` fails, the `mapper` is not executed,
 and the error is preserved. This allows for safe transformation of data within the flow.


## Parameters

- `mapper`: A function of type `'value -> 'next` to transform the successful value.
- `flow`: The source flow of type `Flow` to transform.

## Returns

A new `Flow` with the transformed success value of type `'next`.

