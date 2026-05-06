---
title: catch
description: API reference for TaskFlow.catch
---

# catch

Catches exceptions raised during execution and maps them to a typed error.


```fsharp
let catch (handler: exn -> 'error) (flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value>
```




## Parameters

- `handler`: A function of type `exn -> 'error` to map the exception.
- `flow`: The source task flow.

## Returns

A task flow that converts exceptions into success-path errors.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L415)

