---
title: read
description: API reference for Flow.read
---

# read

Projects a value from the current environment.


```fsharp
let read (projection: 'env -> 'value) : Flow<'env, 'error, 'value>
```


## Remarks

This is the primary way to access dependencies or configuration stored in the environment.
The `projection` function is applied to the environment at execution time.


## Parameters

- `projection`: A function that extracts a value from the environment.

## Returns

A `Flow` containing the projected value.

## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L125)

