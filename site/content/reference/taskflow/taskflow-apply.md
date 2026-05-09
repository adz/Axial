---
title: apply
type: docs
---

Applies a task-flow-wrapped function to a task-flow-wrapped value.


```fsharp
let apply (flow: TaskFlow<'env, 'error, 'value -> 'next>) (value: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'next>
```




## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L480)

