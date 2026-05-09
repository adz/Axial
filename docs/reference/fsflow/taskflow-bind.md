---
title: bind
---

Sequences a task-flow-producing continuation after a successful value.


```fsharp
let bind (binder: 'value -> TaskFlow<'env, 'error, 'next>) (flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'next>
```


## Remarks

This is the "flatmap" operation for `TaskFlow`. It allows for dependent
asynchronous steps where the second flow depends on the value produced by the first.


## Parameters

- `binder`: A function of type `'value -> TaskFlow&lt;'env, 'error, 'next&gt;`.
- `flow`: The source task flow.

## Returns

A new task flow representing the combined workflow.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L330)

