---
title: serviceFromProvider
---

Reads a service from `IServiceProvider` and fails when it is not registered.


```fsharp
let serviceFromProvider<'service> : TaskFlow<IServiceProvider, MissingCapability, 'service>
```




## Information

- **Module**: `Capability`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L836)

