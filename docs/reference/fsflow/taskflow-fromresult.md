---
title: fromResult
---

Lifts a standard `Result` into a task flow.


```fsharp
let fromResult (result: Result<'value, 'error>) : TaskFlow<'env, 'error, 'value>
```




## Parameters

- `result`: The result to lift.

## Returns

A task flow mirroring the result.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L164)

