---
title: mapRuntime
---

Maps the runtime half of a runtime context.


```fsharp
let mapRuntime (mapper: 'runtime -> 'nextRuntime) (context: RuntimeContext<'runtime, 'env>) : RuntimeContext<'nextRuntime, 'env>
```




## Parameters

- `mapper`: A function of type `'runtime -> 'nextRuntime`.
- `context`: The source context.

## Returns

A new context with the mapped runtime services.

## Information

- **Module**: `RuntimeContext`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L69)

