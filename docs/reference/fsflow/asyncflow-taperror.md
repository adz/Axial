---
title: AsyncFlow.tapError
linkTitle: tapError
---

Runs an async side effect on failure and preserves the original error.


```fsharp
let tapError (binder: 'error -> AsyncFlow<'env, 'error, unit>) (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L184)

