---
title: mapEnvironment
type: docs
---

Maps the application environment half of a runtime context.


```fsharp
let mapEnvironment (mapper: 'env -> 'nextEnv) (context: RuntimeContext<'runtime, 'env>) : RuntimeContext<'runtime, 'nextEnv>
```




## Parameters

- `mapper`: A function of type `'env -> 'nextEnv`.
- `context`: The source context.

## Returns

A new context with the mapped environment.

## Information

- **Module**: `RuntimeContext`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L83)

