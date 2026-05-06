---
title: fromOption
description: API reference for Flow.fromOption
---

# fromOption

Lifts an option into a synchronous flow with the supplied error.


```fsharp
let fromOption (error: 'error) (value: 'value option) : Flow<'env, 'error, 'value>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L74)

## Examples

```fsharp
let opt = Some "value"
let flow = Flow.fromOption "missing" opt
```

