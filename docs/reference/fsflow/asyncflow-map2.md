---
title: map2
description: API reference for AsyncFlow.map2
---

# map2

Combines two async flows with a mapping function.


```fsharp
let map2 (mapper: 'left -> 'right -> 'value) (left: AsyncFlow<'env, 'error, 'left>) (right: AsyncFlow<'env, 'error, 'right>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L265)

