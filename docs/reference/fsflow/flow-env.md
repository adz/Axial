---
title: env
---

Reads the current environment as the flow value.


```fsharp
let env<'env, 'error> : Flow<'env, 'error, 'env>
```


## Remarks

Use this when the entire environment object is needed for the next step of the workflow.
For projecting specific properties, `read` is generally more ergonomic.


## Returns

A `Flow` whose successful value is the current environment.

## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L115)

