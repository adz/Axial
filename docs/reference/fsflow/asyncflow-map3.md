---
title: AsyncFlow.map3
linkTitle: map3
---

Combines three async flows with a mapping function.


```fsharp
let map3 (mapper: 'left -> 'middle -> 'right -> 'value) (left: AsyncFlow<'env, 'error, 'left>) (middle: AsyncFlow<'env, 'error, 'middle>) (right: AsyncFlow<'env, 'error, 'right>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L281)

