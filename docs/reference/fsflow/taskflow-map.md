---
title: map
---

Maps the successful value of a task flow.


```fsharp
let map (mapper: 'value -> 'next) (flow: TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'next>
```




## Parameters

- `mapper`: A function of type `'value -> 'next` to transform the success value.
- `flow`: The source task flow of type `TaskFlow`.

## Returns

A new `TaskFlow` with the transformed success value.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L303)

