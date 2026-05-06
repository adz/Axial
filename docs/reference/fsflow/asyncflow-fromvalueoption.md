---
title: fromValueOption
description: API reference for AsyncFlow.fromValueOption
---

# fromValueOption

Lifts a value option into an async flow with the supplied error.


```fsharp
let fromValueOption (error: 'error) (value: 'value voption) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L49)

