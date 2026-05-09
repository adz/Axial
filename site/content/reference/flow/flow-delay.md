---
title: delay
type: docs
---

Defers flow construction until execution time.


```fsharp
let delay (factory: unit -> Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L341)

