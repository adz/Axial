---
title: localEnv
---

Transforms the environment before running a task flow.


```fsharp
let localEnv (mapping: 'outerEnvironment -> 'innerEnvironment) (flow: TaskFlow<'innerEnvironment, 'error, 'value>) : TaskFlow<'outerEnvironment, 'error, 'value>
```




## Parameters

- `mapping`: A function of type `'outerEnvironment -> 'innerEnvironment`.
- `flow`: The task flow expecting `'innerEnvironment`.

## Returns

A task flow that accepts `'outerEnvironment`.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L515)

