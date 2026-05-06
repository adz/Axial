---
title: ensureNotCanceled
description: API reference for TaskFlow.Runtime.ensureNotCanceled
---

# ensureNotCanceled

Returns a typed error immediately when the runtime token is already canceled.


```fsharp
let ensureNotCanceled<'env, 'error> (canceledError: 'error) : TaskFlow<'env, 'error, unit>
```




## Information

- **Module**: `TaskFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L605)

