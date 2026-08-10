---
title: Platform Services
linkTitle: Platform Services
description: Get a live clock, logger, randomness, GUID, and environment-variable bundle into a host.
---

# Platform Services

A host needs the standard operational services before it can run application workflows: a clock, a logger,
randomness, GUIDs, and environment variables. `Axial.PlatformService` bundles all five as `BaseRuntime`.

The shortest path is the live bundle:

```fsharp
open Axial
open Axial.PlatformService
```

```fsharp
let runtime : BaseRuntime = BaseRuntime.liveValue
```

`BaseRuntime` implements `IHas<'service>` for each of the five, so any workflow requiring some combination of them
runs against this single value:

```fsharp
let startup : Flow<BaseRuntime, EnvironmentVariableError, int> =
    flow {
        let! port = (EnvironmentVariable.getInt "PORT" : Flow<BaseRuntime, _, _>)
        do! Log.info $"Listening on {port}"
        return port
    }
```

One bind names the environment explicitly. When a single `flow { }` uses services from two different contracts —
`IEnvironmentVariables` and `ILog` here — the compiler gives each call its own environment variable and then cannot
merge them, because one type cannot be constrained by two `IHas<_>` contracts at once. The annotation on `startup`
does not resolve this: it applies to the result of the `flow { }`, after the body has already been checked.

Naming the type on any one bind is enough, wherever it appears in the block. The rest of the body follows from it.

Use `BaseRuntime.live` when the environment is composed from layers, and `BaseRuntime.fromServiceProvider` when the
host already has an `IServiceProvider` — that variant reports missing registrations as typed `BaseRuntimeError`
startup failures rather than resolution exceptions.

Applications normally extend the bundle with their own services rather than using it bare; see
[building a base runtime](/dependencies/building-a-base-runtime.html).

## Go further

Full documentation for each service — the operation surface, the deterministic test doubles, and the typed
environment-variable error model — is in [platform services](/services/platform-services/index.html) under built-in
services.
