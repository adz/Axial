---
title: Capability.serviceFromProvider
linkTitle: serviceFromProvider
type: docs
---

Reads a service from `IServiceProvider` and fails when it is not registered.


```fsharp
let serviceFromProvider<'service> : Flow<IServiceProvider, MissingCapability, 'service>
```




## Information

- **Module**: `Capability`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L1828)

