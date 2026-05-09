---
title: environment
---

Reads a capability from the application half of a two-context runtime environment.


```fsharp
let environment (projection: 'env -> 'service) : TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'service>
```




## Information

- **Module**: `Capability`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L830)

