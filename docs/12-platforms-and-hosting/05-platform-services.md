---
title: Supplying Platform Services
linkTitle: Supplying Platform Services
description: Get live clock, logger, random, secure-random, GUID, and environment-variable services into a host.
---

# Supplying Platform Services

A host needs the standard operational services before it can run application workflows: a clock, a logger,
ordinary and cryptographically secure randomness, GUIDs, and environment variables. `Axial.PlatformService` bundles all six as `BaseRuntime`.

```fsharp prepare
// Setup for the checked example on this page.
open System
open Axial
open Axial.PlatformService

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

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

`BaseRuntime.live` is a `Layer<unit, Never, BaseRuntime>`, so provision it over a workflow whose environment it
satisfies. The checked run below overrides the clock with a fixed value so the year is deterministic; the layer
shape and the `Layer.provide` call are the same as the real `BaseRuntime.live`:

```fsharp
let currentYear : Flow<BaseRuntime, Never, int> =
    Clock.now |> Flow.map _.Year
```

```fsharp run
let fixedRuntime =
    { BaseRuntime.liveValue with Clock = Clock.fromValue (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)) }

currentYear |> Layer.provide (Layer.succeed fixedRuntime) |> Flow.run () |> function
| Exit.Success year -> shouldEqual 2026 year
| other -> failwithf "Expected a year, got %A" other
```

`BaseRuntime.fromServiceProvider` consumes an `IServiceProvider` and reports a missing registration as
`BaseRuntimeError.MissingService` before the workflow runs; the checked composition and failure are in
[Providing the environment](/dependencies/providing-the-environment.html#take-it-from-a-host-container).

Applications normally extend the bundle with their own services rather than using it bare; see
[building a base runtime](/dependencies/providing-the-environment.html).

## Service details

[Platform services](/services/platform-services/index.html) documents each service's operations, deterministic test doubles, and typed environment-variable error model.
