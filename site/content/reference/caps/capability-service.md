---
title: service
type: docs
---

Reads a capability from a record-based environment projection.


```fsharp
let inline service (projection: 'env -> 'service) : ^flow when ^flow : (static member CapabilityService : ('env -> 'service) -> ^flow)
```


## Remarks

Use this at the edge when a workflow already has a record-shaped environment and only
needs one field, not a full cap-set boundary.


## Information

- **Module**: `Capability`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L819)

