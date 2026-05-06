---
title: traverse
description: API reference for Flow.traverse
---

# traverse

Transforms a sequence of values into a flow and stops at the first failure.


```fsharp
let traverse (mapping: 'value -> Flow<'env, 'error, 'next>) (values: seq<'value>) : Flow<'env, 'error, 'next list>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L345)

