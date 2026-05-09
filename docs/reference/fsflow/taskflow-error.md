---
title: TaskFlow.error
linkTitle: error
---

Creates a failing task flow.


```fsharp
let error (failure: 'error) : TaskFlow<'env, 'error, 'value>
```




## Parameters

- `error`: The failure value of type `'error`.

## Returns

A `TaskFlow` that always fails.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L154)

