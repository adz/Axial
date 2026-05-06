---
title: timeoutToOk
description: API reference for TaskFlow.Runtime.timeoutToOk
---

# timeoutToOk

Returns the supplied success value when the flow times out.


```fsharp
let timeoutToOk (after: TimeSpan) (value: 'value) (flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value>
```




## Information

- **Module**: `TaskFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L694)

