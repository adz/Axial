---
title: run
---

Executes a task flow with the provided environment and cancellation token.


```fsharp
let run (environment: 'env) (cancellationToken: CancellationToken) (TaskFlow operation: TaskFlow<'env, 'error, 'value>) : Task<Result<'value, 'error>>
```




## Parameters

- `environment`: The environment of type `'env`.
- `cancellationToken`: The `CancellationToken`.
- `flow`: The `TaskFlow` to execute.

## Returns

A `Task` containing the `Result`.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L105)

