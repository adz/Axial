---
title: traverse
type: docs
---

Transforms a sequence of values into an async flow and stops at the first failure.


```fsharp
let traverse (mapping: 'value -> AsyncFlow<'env, 'error, 'next>) (values: seq<'value>) : AsyncFlow<'env, 'error, 'next list>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L331)

