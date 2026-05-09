---
title: catchCancellation
type: docs
---

Converts an `OperationCanceledException` into a typed error.


```fsharp
let catchCancellation (handler: OperationCanceledException -> 'error) (flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value>
```




## Information

- **Module**: `TaskFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L592)

