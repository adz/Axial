---
title: Flow.succeed
linkTitle: succeed
---

Alias for `ok` that reads well in some call sites.


```fsharp
let succeed (value: 'value) : Flow<'env, 'error, 'value>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L30)

## Examples

```fsharp
let flow = Flow.succeed 42
let result = Flow.run () flow
// result = Ok 42
```

