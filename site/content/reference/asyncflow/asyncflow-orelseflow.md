---
title: orElseFlow
type: docs
---

Turns a pure validation result into an async flow with synchronous environment-provided failure.


```fsharp
let orElseFlow (errorFlow: Flow<'env, 'error, 'error>) (result: Result<'value, unit>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L70)

