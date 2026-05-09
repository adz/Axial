---
title: RuntimeContext.runtime
linkTitle: runtime
---

Reads the runtime half of a runtime context.


```fsharp
let runtime (context: RuntimeContext<'runtime, 'env>) : 'runtime
```




## Parameters

- `context`: The `RuntimeContext` to read.

## Returns

The runtime services of type `'runtime`.

## Information

- **Module**: `RuntimeContext`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L53)

