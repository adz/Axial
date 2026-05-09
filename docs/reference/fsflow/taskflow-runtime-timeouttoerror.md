---
title: TaskFlow.Runtime.timeoutToError
linkTitle: timeoutToError
---

Forwards to `timeout` for a typed failure on timeout.


```fsharp
let timeoutToError (after: TimeSpan) (error: 'error) (flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value>
```




## Information

- **Module**: `TaskFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L712)

