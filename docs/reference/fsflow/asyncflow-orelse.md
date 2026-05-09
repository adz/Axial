---
title: orElse
---

Falls back to another async flow when the source flow fails.


```fsharp
let orElse (fallback: AsyncFlow<'env, 'error, 'value>) (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L247)

