---
title: mapError
description: API reference for TaskFlow.mapError
---

# mapError

Maps the error value of a task flow.


```fsharp
let mapError (mapper: 'error -> 'nextError) (flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'nextError, 'value>
```




## Parameters

- `mapper`: A function of type `'error -> 'nextError`.
- `flow`: The source task flow.

## Returns

A new `TaskFlow` with the transformed error type.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L396)

