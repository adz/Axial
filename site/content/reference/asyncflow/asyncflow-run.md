---
title: run
type: docs
---

Executes an async flow with the provided environment.


```fsharp
let run (environment: 'env) (AsyncFlow operation: AsyncFlow<'env, 'error, 'value>) : Async<Result<'value, 'error>>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L8)

