---
title: Platform Services
description: The clock, logging, randomness, GUID, and environment-variable services that make ambient state explicit.
---

`Axial.PlatformService` covers the five capabilities that would otherwise be read straight from static globals:
`DateTimeOffset.UtcNow`, a logger, `Random`, `Guid.NewGuid()`, and `Environment.GetEnvironmentVariable`. Each becomes
a declared dependency, which is what makes a workflow that reads the time or generates an identifier reproducible in
a test.

```fsharp
open System
open Axial
open Axial.PlatformService
```

| Service | Replaces | Module |
| --- | --- | --- |
| `IClock` | `DateTimeOffset.UtcNow` | `Clock` |
| `ILog` | An ambient logger | `Log` |
| `IRandom` | `System.Random` | `Random` |
| `IGuid` | `Guid.NewGuid()` | `Guid` |
| `IEnvironmentVariables` | `Environment.GetEnvironmentVariable` | `EnvironmentVariables`, `EnvironmentVariable` |

These are the smallest services Axial ships, and the most valuable to make explicit. A function that reads the clock
directly cannot be tested at a chosen instant; one that declares `IHasClock` can:

```fsharp
let isExpired (expiry: DateTimeOffset) : Flow<#IHasClock, Never, bool> =
    Clock.now |> Flow.map (fun now -> now >= expiry)
```

## The base runtime

Applications rarely want one of these — they want all five. `BaseRuntime` is the record that bundles them, and it
implements one contract per service, so a workflow requiring any combination is satisfied by the single value:

```fsharp
let liveRuntime : BaseRuntime = BaseRuntime.liveValue
```

`BaseRuntime.live : Layer<unit, Never, BaseRuntime>` provides the same bundle to a layer-composed runtime, and
`BaseRuntime.fromServiceProvider : Layer<IServiceProvider, BaseRuntimeError, BaseRuntime>` builds it from a host
container, turning missing registrations into typed startup errors.

Most applications extend `BaseRuntime` rather than replacing it — see
[Tutorial: Composing Built-in Services](/services/existing-services.html) for the composition, and
[platform services getting started](/platforms-and-hosting/platform-services.html) for the shortest path to a
running host.

## Deterministic implementations

Every module ships test doubles beside its `live` value, so a test rarely needs to write an object expression:

```fsharp
let fixedRuntime : BaseRuntime =
    { Clock = Clock.fromValue (DateTimeOffset.Parse "2026-01-01T00:00:00Z")
      Log = Log.live
      Random = Random.fromValue 7
      Guid = Guid.fromValue (System.Guid.Parse "00000000-0000-0000-0000-000000000001")
      EnvironmentVariables = EnvironmentVariables.fromPairs [ "AXIAL_ENV", "test" ] }
```

`Clock.fromValue`, `Guid.fromValue`, `Random.fromValue`, `Random.fromFixed`, `EnvironmentVariables.fromPairs`, and
`Log.fromSink` each pin one service to a known answer.

## In this section

1. [Clock](clock.html) — the current instant, and why it belongs in the environment.
2. [Logging](logging.html) — `ILog` levels, sinks, and its relationship to telemetry.
3. [Randomness and GUIDs](random-and-guid.html) — non-determinism you can pin in a test.
4. [Environment variables](environment-variables.html) — typed reads with `EnvironmentVariableError`.
