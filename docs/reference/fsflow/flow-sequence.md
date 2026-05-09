---
title: sequence
---

Transforms a sequence of flows into a flow of a sequence and stops at the first failure.


```fsharp
let sequence (flows: seq<Flow<'env, 'error, 'value>>) : Flow<'env, 'error, 'value list>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L364)

