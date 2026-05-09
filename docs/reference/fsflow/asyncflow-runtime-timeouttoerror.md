---
title: timeoutToError
---

Transitions to a failure value on timeout.


```fsharp
let timeoutToError (after: TimeSpan) (error: 'error) (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L549)

