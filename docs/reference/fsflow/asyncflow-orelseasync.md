---
title: orElseAsync
---

Turns a pure validation result into an async flow with async-provided failure.


```fsharp
let orElseAsync (errorAsync: Async<'error>) (result: Result<'value, unit>) : AsyncFlow<'env, 'error, 'value>
```




## Returns

An `AsyncFlow` that mirrors the result or produces the async error.

## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L56)

