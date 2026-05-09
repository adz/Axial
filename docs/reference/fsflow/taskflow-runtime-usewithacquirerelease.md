---
title: TaskFlow.Runtime.useWithAcquireRelease
linkTitle: useWithAcquireRelease
---

Acquires a resource, uses it, and always runs the release action.


```fsharp
let useWithAcquireRelease (acquire: TaskFlow<'env, 'error, 'resource>) (release: 'resource -> CancellationToken -> Task) (useResource: 'resource -> TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value>
```




## Information

- **Module**: `TaskFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L651)

