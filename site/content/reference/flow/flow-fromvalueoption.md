---
title: fromValueOption
type: docs
---

Lifts a value option into a synchronous flow with the supplied error.


```fsharp
let fromValueOption (error: 'error) (value: 'value voption) : Flow<'env, 'error, 'value>
```




## Parameters

- `error`: The error to return if the value option is `ValueNone`.
- `value`: The value option to lift.

## Returns

A `Flow` that succeeds with the option's value or fails with the provided error.

## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L83)

