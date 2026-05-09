---
title: TaskFlow.toTask
linkTitle: toTask
---

Converts a task flow into a hot `Task`.


```fsharp
let toTask (environment: 'env) (cancellationToken: CancellationToken) (flow: TaskFlow<'env, 'error, 'value>) : Task<Result<'value, 'error>>
```


## Remarks

This is an alias for `run` that emphasizes the conversion to a standard .NET Task.


## Parameters

- `environment`: The environment of type `'env`.
- `cancellationToken`: The `CancellationToken`.
- `flow`: The task flow to convert.

## Returns

A started task.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L130)

