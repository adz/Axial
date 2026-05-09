---
title: AsyncFlow.fromAsyncResult
linkTitle: fromAsyncResult
---

Lifts an async result into an async flow.


```fsharp
let fromAsyncResult (operation: Async<Result<'value, 'error>>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L114)

