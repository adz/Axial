---
title: TaskFlow.readRuntime
linkTitle: readRuntime
---

Reads the runtime half of a runtime-context environment.


```fsharp
let readRuntime (projection: 'runtime -> 'value) : TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value>
```




## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L288)

