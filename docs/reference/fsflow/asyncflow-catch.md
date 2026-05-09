---
title: catch
---

Catches exceptions raised during execution and maps them to a typed error.


```fsharp
let catch (handler: exn -> 'error) (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L219)

