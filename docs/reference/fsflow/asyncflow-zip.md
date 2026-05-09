---
title: AsyncFlow.zip
linkTitle: zip
---

Combines two async flows into a tuple of their values.


```fsharp
let zip (left: AsyncFlow<'env, 'error, 'left>) (right: AsyncFlow<'env, 'error, 'right>) : AsyncFlow<'env, 'error, 'left * 'right>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L254)

