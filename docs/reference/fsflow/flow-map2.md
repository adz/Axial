---
title: map2
---

Combines two flows with a mapping function.


```fsharp
let map2 (mapper: 'left -> 'right -> 'value) (left: Flow<'env, 'error, 'left>) (right: Flow<'env, 'error, 'right>) : Flow<'env, 'error, 'value>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L283)

