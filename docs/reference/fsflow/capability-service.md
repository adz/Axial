---
title: service
description: API reference for Capability.service
---

# service

Reads a capability from a record-based environment projection.


```fsharp
let inline service (projection: 'env -> 'service) : ^flow when ^flow : (static member CapabilityService : ('env -> 'service) -> ^flow)
```




## Information

- **Module**: `Capability`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L815)

