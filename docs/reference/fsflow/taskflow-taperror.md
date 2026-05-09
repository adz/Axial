---
title: TaskFlow.tapError
linkTitle: tapError
---

Runs a task-based side effect on failure and preserves the original error.


```fsharp
let tapError (binder: 'error -> TaskFlow<'env, 'error, unit>) (flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value>
```




## Parameters

- `binder`: A function that produces a side-effect task flow from the error value.
- `flow`: The source task flow.

## Returns

A task flow that preserves the original error after the side effect.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L374)

