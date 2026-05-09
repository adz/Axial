---
title: fromResult
---

Lifts a `Result` into a synchronous flow.


```fsharp
let fromResult (result: Result<'value, 'error>) : Flow<'env, 'error, 'value>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L64)

## Examples

```fsharp
let res = Ok "success"
let flow = Flow.fromResult res
```

