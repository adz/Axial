---
title: map2
type: docs
---

Combines two task flows with a mapping function.


```fsharp
let map2 (mapper: 'left -> 'right -> 'value) (left: TaskFlow<'env, 'error, 'left>) (right: TaskFlow<'env, 'error, 'right>) : TaskFlow<'env, 'error, 'value>
```




## Parameters

- `mapper`: A function of type `'left -> 'right -> 'value`.
- `left`: The first task flow.
- `right`: The second task flow.

## Returns

A task flow with the combined value.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L471)

