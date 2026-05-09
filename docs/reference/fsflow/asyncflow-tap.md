---
title: AsyncFlow.tap
linkTitle: tap
---

Runs an async side effect on success and preserves the original value.


```fsharp
let tap (binder: 'value -> AsyncFlow<'env, 'error, unit>) (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L173)

