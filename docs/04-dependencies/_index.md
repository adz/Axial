---
title: Dependencies
description: Declare what a workflow needs, supply it at the edge, and manage scoped resources.
---

# Dependencies

**Pass Flow a record.** A workflow states what it needs in its environment channel; you build that record and hand
it over when the workflow runs:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
type AppEnv =
    { Users: IUserStore
      Audit: IAuditLog }

let loadUser id : EnvFlow<AppEnv, User> =
    flow {
        let! users = Flow.read _.Users
        return! users.Load id
    }

let exit = loadUser userId |> Flow.run { Users = liveUsers; Audit = liveAudit }
```

That is the whole mechanism for most applications. There is no container, no registration, and no resolution step —
a record is a record, and a test supplies a different one.

Two things build on it, and neither is needed to start. **Contracts** let a *package* ask for a service without
knowing your record type; that is how `Console.writeLine` and the rest of the
[built-in services](/services/index.html) work, and how you would publish your own. **Layers** are for provisioning
that is itself effectful — see [layers](/layers/index.html), a separate package.

## In this section

1. [Getting started](getting-started-dependencies.html) — records, `Flow.read`, and when an environment earns its
   keep.
2. [Dependencies](dependencies.html) — arguments, records, named services, and layers compared.
3. [Explicit services](explicit-services.html) — per-service contracts and their accessors.
4. [Scopes and resources](scopes-and-resources.html) — deterministic cleanup.
5. [Service provider boundaries](service-provider-boundaries.html) — meeting `IServiceProvider` at the host edge.
6. [Building a base runtime](building-a-base-runtime.html) — assembling the environment an application runs on.
7. [Tutorials](tutorials/index.html) — the same material worked end to end.

For the services Axial already implements — the clock, console, file system, processes, and HTTP — see
[built-in services](/services/index.html).
