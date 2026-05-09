---
title: delay
---

Defers async flow construction until execution time.


```fsharp
let delay (factory: unit -> AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L327)

