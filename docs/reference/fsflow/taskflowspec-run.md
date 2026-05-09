---
title: run
---

Runs the spec with the supplied cancellation token.


```fsharp
let run (cancellationToken: CancellationToken) (spec: TaskFlowSpec<'runtime, 'env, 'error, 'value>) : Task<Result<'value, 'error>>
```




## Information

- **Module**: `TaskFlowSpec`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L801)

