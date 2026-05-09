---
title: fromValueOption
type: docs
---

Lifts a value option into a task flow with the supplied error.


```fsharp
let fromValueOption (error: 'error) (value: 'value voption) : TaskFlow<'env, 'error, 'value>
```




## Parameters

- `error`: The error of type `'error` to return if the value option is `ValueNone`.
- `value`: The value option to lift.

## Returns

A task flow succeeding with the option's value or failing.

## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L180)

