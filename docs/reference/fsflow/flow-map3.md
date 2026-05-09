---
title: map3
---

Combines three flows with a mapping function.


```fsharp
let map3 (mapper: 'left -> 'middle -> 'right -> 'value) (left: Flow<'env, 'error, 'left>) (middle: Flow<'env, 'error, 'middle>) (right: Flow<'env, 'error, 'right>) : Flow<'env, 'error, 'value>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L299)

