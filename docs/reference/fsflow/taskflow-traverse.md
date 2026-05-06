---
title: traverse
description: API reference for TaskFlow.traverse
---

# traverse

Transforms a sequence of values into a task flow and stops at the first failure.


```fsharp
let traverse (mapping: 'value -> TaskFlow<'env, 'error, 'next>) (values: seq<'value>) : TaskFlow<'env, 'error, 'next list>
```




## Parameters

- `mapping`: A function of type `'value -> TaskFlow&lt;'env, 'error, 'next&gt;`.
- `values`: The input sequence.

## Returns

A task flow containing the list of successful results.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L540)

