---
title: "TaskFlow"
type: docs
weight: 30
---

Represents a cold task-based workflow that reads an environment, observes a runtime cancellation token,
returns a typed result, and is executed explicitly through `TaskFlow.run`.


```fsharp
type TaskFlow<'env, 'error, 'value>
```




## Definitions

### `type TaskFlow<'env, 'error, 'value>`

- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L15)

### `type TaskFlow<'env, 'error, 'value> with static member CapabilityService (projection: 'env -> 'service) : TaskFlow<'env, 'error, 'service>`

- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L26)

## Information

- **Module**: Global
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L15)

