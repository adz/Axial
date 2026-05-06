---
title: orElseAsync
description: API reference for TaskFlow.orElseAsync
---

# orElseAsync

Turns a pure validation result into a task flow with task-provided failure.


```fsharp
let orElseAsync (errorAsync: Async<'error>) (result: Result<'value, unit>) : TaskFlow<'env, 'error, 'value>
```




## Returns

A `TaskFlow` that mirrors the result or produces the task error.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L200)

