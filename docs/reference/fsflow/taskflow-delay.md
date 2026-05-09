---
title: delay
---

Defers task flow construction until execution time.


```fsharp
let delay (factory: unit -> TaskFlow<'env, 'error, 'value>) : TaskFlow<'env, 'error, 'value>
```




## Parameters

- `factory`: A function of type `unit -> TaskFlow&lt;'env, 'error, 'value&gt;`.

## Returns

A task flow that evaluates the factory only when executed.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L529)

