---
title: timeoutWith
type: docs
---

Transitions to a fallback workflow on timeout.


```fsharp
let timeoutWith (after: TimeSpan) (fallback: unit -> AsyncFlow<'env, 'error, 'value>) (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L559)

