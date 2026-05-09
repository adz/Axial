---
title: useWithAcquireRelease
type: docs
---

Safely acquires a resource, uses it, and ensures it is released via a task-based action.


```fsharp
let useWithAcquireRelease (acquire: AsyncFlow<'env, 'error, 'resource>) (release: 'resource -> CancellationToken -> Task) (useResource: 'resource -> AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value>
```




## Parameters

- `acquire`: The flow that acquires the resource.
- `release`: The function that releases the resource.
- `useResource`: The flow that uses the resource.

## Information

- **Module**: `AsyncFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L473)

