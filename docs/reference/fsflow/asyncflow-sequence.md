---
title: AsyncFlow.sequence
linkTitle: sequence
---

Transforms a sequence of async flows into an async flow of a sequence and stops at the first failure.


```fsharp
let sequence (flows: seq<AsyncFlow<'env, 'error, 'value>>) : AsyncFlow<'env, 'error, 'value list>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L354)

