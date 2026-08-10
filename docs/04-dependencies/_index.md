---
title: Dependencies
description: Declare what a workflow needs, supply it at the edge, and manage scoped resources.
---

# Dependencies

Start with ordinary function arguments. Reach for an environment when several workflows need the same dependencies
and threading them through unrelated callers has become noise.

**Then pass Flow a record.** A workflow states what it needs in its environment channel; you build that record and
hand it over when the workflow runs:

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
a record is a record, and a test supplies a different one with fakes in place of the live services.

Two things build on it, and neither is needed to start. **Contracts** let a *package* ask for a service without
knowing your record type; that is how `Console.writeLine` and the rest of the
[built-in services](/services/index.html) work, and how you would publish your own. **Layers** are for provisioning
that is itself effectful — see [layers](/layers/index.html), a separate package.

## In this section

1. [The environment](the-environment.html) — what `'env` actually is, and the functions that read it.
2. [Choosing an approach](choosing-an-approach.html) — arguments, records, contracts, and layers compared.
3. [Service contracts](service-contracts.html) — how a package asks for a dependency it cannot name.
4. [Providing the environment](providing-the-environment.html) — building the value at a host boundary.
5. [Tutorials](tutorials/index.html) — the same material worked end to end.

For the services Axial already implements — the clock, console, file system, processes, and HTTP — see
[built-in services](/services/index.html).
