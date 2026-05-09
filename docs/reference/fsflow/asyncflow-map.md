---
title: map
---

Maps the successful value of an async flow.


```fsharp
let map (mapper: 'value -> 'next) (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'next>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L126)

