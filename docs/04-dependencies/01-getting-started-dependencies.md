---
title: Dependencies
description: Read explicit application dependencies from a Flow environment.
---

# Dependencies

Start with ordinary function arguments. Add an environment when several workflows need the same application
dependencies and passing them through unrelated callers has become noise.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
type AppEnv =
    { Users: IUserStore
      Audit: IAuditLog }
```

`Flow.read` projects the dependency needed by the current operation:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let loadUser id : EnvFlow<AppEnv, User> =
    flow {
        let! users = Flow.read _.Users
        return! users.Load id
    }
```

The environment appears in the Flow type, so callers can see the requirement. The concrete value is supplied once
when the workflow runs:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let exit = loadUser userId |> Flow.run liveEnv
```

Tests provide a different record using the same shape. There is no hidden service locator in the workflow.

Records plus `Flow.read` are the default, and for most applications the end of the story. Contracts matter when a
*package* must ask for a service without knowing your record type; layers matter when building the environment is
itself effectful. Neither is needed to start.

## Go Further

- [Dependencies](/dependencies/dependencies.html) compares arguments, records, named
  services, and Layers.
- [App Record tutorial](/dependencies/tutorials/app-record.html) builds a feature over a concrete environment record.
- [Creating Reusable Services](/dependencies/tutorials/custom-services.html) introduces nominal service
  contracts when helpers must be shared across environment shapes.
- [Layers](/layers/index.html) covers provisioning that needs flow capabilities to build — a separate package, and
  not required for ordinary wiring.
