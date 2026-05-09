---
title: bind
---

Sequences a synchronous continuation after a successful value.


```fsharp
let bind (binder: 'value -> Flow<'env, 'error, 'next>) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'next>
```


## Remarks

This is the "flatmap" operation for `Flow`. It allows for dependent
steps where the second flow depends on the value produced by the first.


## Parameters

- `binder`: A function that takes the successful value and returns a new flow.
- `flow`: The source flow to sequence.

## Returns

A `Flow` representing the combined workflow.

## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L154)

