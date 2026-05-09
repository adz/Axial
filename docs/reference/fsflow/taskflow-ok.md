---
title: TaskFlow.ok
linkTitle: ok
---

Creates a successful task flow.


```fsharp
let ok (value: 'value) : TaskFlow<'env, 'error, 'value>
```




## Parameters

- `value`: The success value of type `'value`.

## Returns

A `TaskFlow` that always succeeds.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L140)

