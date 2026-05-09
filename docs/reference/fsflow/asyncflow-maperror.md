---
title: AsyncFlow.mapError
linkTitle: mapError
---

Maps the error value of an async flow.


```fsharp
let mapError (mapper: 'error -> 'nextError) (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'nextError, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L203)

