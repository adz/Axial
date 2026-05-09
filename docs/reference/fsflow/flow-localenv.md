---
title: localEnv
---

Transforms the environment before running the flow.


```fsharp
let localEnv (mapping: 'outerEnvironment -> 'innerEnvironment) (flow: Flow<'innerEnvironment, 'error, 'value>) : Flow<'outerEnvironment, 'error, 'value>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L324)

