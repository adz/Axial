---
title: runContext
type: docs
---

Runs a task flow against a `RuntimeContext` and its internal cancellation token.


```fsharp
let runContext (context: RuntimeContext<'runtime, 'env>) (flow: TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value>) : Task<Result<'value, 'error>>
```




## Parameters

- `context`: The `RuntimeContext` providing services and cancellation.
- `flow`: The task flow to run.

## Returns

A `Task` with the final result.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L116)

