---
title: provideLayer
description: API reference for Layer.provideLayer
---

# provideLayer

Provides a derived environment from a layer flow to a downstream flow.


```fsharp
let inline provideLayer (layer: ^layer) (flow: ^flow) : ^flow when ^flow : (static member ProvideLayer : ^layer * ^flow -> ^flow)
```




## Information

- **Module**: `Layer`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L848)

