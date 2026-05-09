---
title: create
---

Creates a task-flow spec from runtime services, application dependencies, and a build function.


```fsharp
let create (runtime: 'runtime) (environment: 'env) (build: unit -> TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'value>) : TaskFlowSpec<'runtime, 'env, 'error, 'value>
```




## Information

- **Module**: `TaskFlowSpec`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L789)

