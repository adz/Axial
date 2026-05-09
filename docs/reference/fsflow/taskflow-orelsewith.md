---
title: TaskFlow.orElseWith
linkTitle: orElseWith
---

Falls back to another task flow when the source flow fails.


```fsharp
let orElseWith (fallback: 'error -> TaskFlow<'env, 'error, 'value>) (flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value>
```




## Parameters

- `fallback`: The fallback flow of type `TaskFlow`.
- `flow`: The primary task flow.

## Returns

A task flow that tries the primary first, then the fallback.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L432)

