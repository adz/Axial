---
title: provideLayer
---

Provides a derived environment from a layer flow to a downstream flow.


```fsharp
let provideLayer (layer: Flow<'input, 'error, 'environment>) (flow: Flow<'environment, 'error, 'value>) : Flow<'input, 'error, 'value>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L331)

