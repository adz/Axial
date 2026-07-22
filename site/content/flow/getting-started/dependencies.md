---
weight: 9
title: Dependencies
description: Read explicit application dependencies from a Flow environment.
type: docs
---


Start with ordinary function arguments. Add an environment when several workflows need the same application
dependencies and passing them through unrelated callers has become noise.

```fsharp
type AppEnv =
    { Users: IUserStore
      Audit: IAuditLog }
```

`Flow.read` projects the dependency needed by the current operation:

```fsharp
let loadUser id : EnvFlow<AppEnv, User> =
    flow {
        let! users = Flow.read _.Users
        return! users.Load id
    }
```

The environment appears in the Flow type, so callers can see the requirement. The concrete value is supplied once
when the workflow runs:

```fsharp
let! exit = (loadUser userId).ToTask(liveEnv)
```

Tests provide a different record using the same shape. There is no hidden service locator in the workflow.

Records plus `Flow.read` are the default for application code. Named services and Layers are useful later for shared
libraries, provisioning, startup failure, or scope-owned resources.

Continue with the [Explicit Dependencies tutorial]({{< relref "/flow/tutorials/explicit-dependencies/" >}}) before
introducing Layers.
