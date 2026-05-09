---
title: TaskFlow.tap
linkTitle: tap
---

Runs a task-based side effect on success and preserves the original value.


```fsharp
let tap (binder: 'value -> TaskFlow<'env, 'error, unit>) (flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value>
```




## Parameters

- `binder`: A function that produces a side-effect task flow from the successful value.
- `flow`: The source task flow.

## Returns

A task flow that preserves the original success value after the side effect.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L360)

