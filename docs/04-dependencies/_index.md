---
title: Dependencies
description: Declare what a workflow needs, supply it at the edge, and manage scoped resources.
---

# Dependencies

This section is about declaring your own dependencies. A Flow states what it needs in its environment channel, and
the value is supplied once, at the edge, when the workflow runs:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let loadUser id : EnvFlow<AppEnv, User> =
    flow {
        let! users = Flow.read _.Users
        return! users.Load id
    }
```

Start with records and `Flow.read`. Reach for named service contracts, layers, and scopes when a shared library,
provisioning failure, or a resource that must be released deterministically makes the simpler form insufficient.

## In this section

1. [Getting started](getting-started-dependencies.html) — records, `Flow.read`, and when an environment earns its
   keep.
2. [Dependencies](dependencies.html) — arguments, records, named services, and layers compared.
3. [Explicit services](explicit-services.html) — per-service contracts and their accessors.
4. [Scopes and resources](scopes-and-resources.html) — deterministic cleanup.
5. [Service provider boundaries](service-provider-boundaries.html) — meeting `IServiceProvider` at the host edge.
6. [Building a base runtime](building-a-base-runtime.html) — assembling the environment an application runs on.
7. [Tutorials](tutorials/index.html) — the same material worked end to end.

Provisioning that itself needs flow capabilities lives in [layers](/layers/index.html), a separate package.

For the services Axial already implements — console, file system, and processes — see
[built-in services](/services/index.html).
