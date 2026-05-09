---
title: TaskFlow.zip
linkTitle: zip
---

Combines two task flows into a tuple of their values.


```fsharp
let zip (left: TaskFlow<'env, 'error, 'left>) (right: TaskFlow<'env, 'error, 'right>) : TaskFlow<'env, 'error, 'left * 'right>
```




## Parameters

- `left`: The first task flow.
- `right`: The second task flow.

## Returns

A task flow containing a tuple of results.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L456)

