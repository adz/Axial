---
title: cancellationToken
---

Reads the cancellation token stored in a runtime context.


```fsharp
let cancellationToken (context: RuntimeContext<'runtime, 'env>) : CancellationToken
```




## Parameters

- `context`: The `RuntimeContext` to read.

## Returns

The `CancellationToken`.

## Information

- **Module**: `RuntimeContext`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L63)

