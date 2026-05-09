---
title: fromFlow
---

Lifts a synchronous flow into an async flow.


```fsharp
let fromFlow (flow: Flow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L102)

