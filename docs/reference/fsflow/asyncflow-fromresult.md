---
title: fromResult
---

Lifts a `Result` into an async flow.


```fsharp
let fromResult (result: Result<'value, 'error>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L39)

