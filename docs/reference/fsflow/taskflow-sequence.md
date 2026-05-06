---
title: sequence
description: API reference for TaskFlow.sequence
---

# sequence

Transforms a sequence of task flows into a task flow of a sequence and stops at the first failure.


```fsharp
let sequence (flows: seq<TaskFlow<'env, 'error, 'value>>) : TaskFlow<'env, 'error, 'value list>
```




## Parameters

- `flows`: A sequence of task flows.

## Returns

A task flow containing the list of successful results.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L565)

