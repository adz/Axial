---
title: ColdTask
linkTitle: ColdTask
---

Represents delayed task work that can observe a runtime cancellation token when it is started.


```fsharp
type ColdTask<'value>
```




## Constructors

- `ColdTask of (CancellationToken -> Task<'value>)` [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L24)

## Information

- **Module**: Global
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L23)

