---
title: sleep
type: docs
---

Suspends the flow for the specified duration while observing cancellation.


```fsharp
let sleep<'env, 'error> (delay: TimeSpan) : TaskFlow<'env, 'error, unit>
```




## Information

- **Module**: `TaskFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L613)

