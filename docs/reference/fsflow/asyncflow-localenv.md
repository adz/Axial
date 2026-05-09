---
title: localEnv
---

Transforms the environment before running the async flow.


```fsharp
let localEnv (mapping: 'outerEnvironment -> 'innerEnvironment) (flow: AsyncFlow<'innerEnvironment, 'error, 'value>) : AsyncFlow<'outerEnvironment, 'error, 'value>
```




## Information

- **Module**: `AsyncFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L306)

