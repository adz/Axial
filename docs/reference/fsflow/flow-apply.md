---
title: apply
description: API reference for Flow.apply
---

# apply

Applies a flow-wrapped function to a flow-wrapped value.


```fsharp
let apply (flow: Flow<'env, 'error, 'value -> 'next>) (value: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'next>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L292)

