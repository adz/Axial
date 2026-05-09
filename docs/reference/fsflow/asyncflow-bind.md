---
title: bind
---

Sequences an async continuation after a successful value.


```fsharp
let bind (binder: 'value -> AsyncFlow<'env, 'error, 'next>) (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'next>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L146)

