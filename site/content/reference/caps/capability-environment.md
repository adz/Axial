---
title: Capability.environment
linkTitle: environment
type: docs
---

Reads a capability from the application half of a two-context runtime environment.


```fsharp
let environment (projection: 'env -> 'service) : Flow<RuntimeContext<'runtime, 'env>, 'error, 'service>
```




## Information

- **Module**: `Capability`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L1822)

