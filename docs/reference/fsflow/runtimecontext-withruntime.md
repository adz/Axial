---
title: RuntimeContext.withRuntime
linkTitle: withRuntime
---

Replaces the runtime half of a runtime context.


```fsharp
let withRuntime (runtime: 'nextRuntime) (context: RuntimeContext<'runtime, 'env>) : RuntimeContext<'nextRuntime, 'env>
```




## Parameters

- `runtime`: The new runtime services.
- `context`: The source context.

## Returns

A new context with the replaced runtime services.

## Information

- **Module**: `RuntimeContext`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L97)

