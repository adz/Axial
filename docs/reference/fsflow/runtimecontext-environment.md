---
title: environment
---

Reads the application environment half of a runtime context.


```fsharp
let environment (context: RuntimeContext<'runtime, 'env>) : 'env
```




## Parameters

- `context`: The `RuntimeContext` to read.

## Returns

The application environment of type `'env`.

## Information

- **Module**: `RuntimeContext`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L58)

