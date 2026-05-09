---
title: fromOption
---

Lifts an option into a task flow with the supplied error.


```fsharp
let fromOption (error: 'error) (value: 'value option) : TaskFlow<'env, 'error, 'value>
```




## Parameters

- `error`: The error to return if the option is `None`.
- `value`: The option to lift.

## Returns

A task flow succeeding with the option's value or failing.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L171)

