---
title: withEnvironment
---

Replaces the environment half of a runtime context.


```fsharp
let withEnvironment (environment: 'nextEnv) (context: RuntimeContext<'runtime, 'env>) : RuntimeContext<'runtime, 'nextEnv>
```




## Parameters

- `environment`: The new application environment.
- `context`: The source context.

## Returns

A new context with the replaced environment.

## Information

- **Module**: `RuntimeContext`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L107)

