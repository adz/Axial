---
title: Capability.runtime
linkTitle: runtime
---

Reads a capability from the runtime half of a two-context runtime environment.


```fsharp
let runtime (projection: 'runtime -> 'service) : TaskFlow<RuntimeContext<'runtime, 'env>, 'error, 'service>
```




## Information

- **Module**: `Capability`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L824)

