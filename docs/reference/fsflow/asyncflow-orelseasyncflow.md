---
title: AsyncFlow.orElseAsyncFlow
linkTitle: orElseAsyncFlow
---

Turns a pure validation result into an async flow whose failure value comes from another async flow.


```fsharp
let orElseAsyncFlow (errorFlow: AsyncFlow<'env, 'error, 'error>) (result: Result<'value, unit>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L85)

