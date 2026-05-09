---
title: TaskFlow.orElseTask
linkTitle: orElseTask
---




```fsharp
let orElseTask (errorTask: Task<'error>) (result: Result<'value, unit>) : TaskFlow<'env, 'error, 'value>
```




## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L185)

