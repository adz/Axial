---
title: Supplying Platform Services
linkTitle: Supplying Platform Services
description: Get a live clock, logger, randomness, GUID, and environment-variable bundle into a host.
---

# Supplying Platform Services

A host needs the standard operational services before it can run application workflows: a clock, a logger,
randomness, GUIDs, and environment variables. `Axial.PlatformService` bundles all five as `BaseRuntime`.

Use the live bundle:

```fsharp
open Axial
open Axial.PlatformService
```

```fsharp
let runtime : BaseRuntime = BaseRuntime.liveValue
```

`BaseRuntime` implements one contract per service, so any workflow requiring some combination of them
runs against this single value:

```fsharp
let startup : Flow<BaseRuntime, EnvironmentVariableError, int> =
    flow {
        let! port = EnvironmentVariable.getInt "PORT"
        do! Log.info $"Listening on {port}"
        return port
    }
```

The block does not name the service types. `IHasEnvironmentVariables` and `IHasLog` are distinct contracts, so their constraints merge.

Use `BaseRuntime.live` when the environment is composed from layers, and `BaseRuntime.fromServiceProvider` when the
host already has an `IServiceProvider`; that variant reports missing registrations as typed `BaseRuntimeError`
startup failures rather than resolution exceptions.

Applications normally extend the bundle with their own services rather than using it bare; see
[building a base runtime](/dependencies/providing-the-environment.html).

## Service details

[Platform services](/services/platform-services/index.html) documents each service's operations, deterministic test doubles, and typed environment-variable error model.
