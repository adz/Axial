---
title: Flow.fail
linkTitle: fail
---

Alias for `error` that reads well in some call sites.


```fsharp
let fail (failure: 'error) : Flow<'env, 'error, 'value>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L54)

## Examples

```fsharp
let flow = Flow.fail "error"
let result = Flow.run () flow
// result = Error "error"
```

