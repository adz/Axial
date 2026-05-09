---
title: orElseWith
type: docs
---

Falls back to another flow when the source flow fails.


```fsharp
let orElseWith (fallback: 'error -> Flow<'env, 'error, 'value>) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L255)

