---
title: fromOption
description: API reference for AsyncFlow.fromOption
---

# fromOption

Lifts an option into an async flow with the supplied error.


```fsharp
let fromOption (error: 'error) (value: 'value option) : AsyncFlow<'env, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L43)

