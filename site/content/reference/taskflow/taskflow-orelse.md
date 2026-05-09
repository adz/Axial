---
title: orElse
type: docs
---

Falls back to another task flow when the source flow fails.


```fsharp
let orElse (fallback: TaskFlow<'env, 'error, 'value>) (flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value>
```




## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L446)

