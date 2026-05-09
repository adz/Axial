---
title: apply
type: docs
---

Applies an async-flow-wrapped function to an async-flow-wrapped value.


```fsharp
let apply (flow: AsyncFlow<'env, 'error, 'value -> 'next>) (value: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'next>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L274)

