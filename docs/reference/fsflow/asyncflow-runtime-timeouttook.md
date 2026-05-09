---
title: timeoutToOk
---

Wraps a flow with a timeout. If the flow does not complete within the specified duration, returns a success value.


```fsharp
let timeoutToOk (after: TimeSpan) (value: 'value) (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L525)

