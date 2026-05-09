---
title: zip
type: docs
---

Combines two flows into a tuple of their values.


```fsharp
let zip (left: Flow<'env, 'error, 'left>) (right: Flow<'env, 'error, 'right>) : Flow<'env, 'error, 'left * 'right>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L272)

