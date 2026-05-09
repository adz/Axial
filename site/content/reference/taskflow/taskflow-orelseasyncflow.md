---
title: orElseAsyncFlow
type: docs
---




```fsharp
let orElseAsyncFlow (errorFlow: AsyncFlow<'env, 'error, 'error>) (result: Result<'value, unit>) : TaskFlow<'env, 'error, 'value>
```




## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L227)

