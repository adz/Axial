---
title: readEnvironment
description: API reference for TaskFlow.readEnvironment
---

# readEnvironment

Reads the application environment half of a runtime-context environment.


```fsharp
let readEnvironment (projection: 'env -> 'value) : TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value>
```




## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L294)

