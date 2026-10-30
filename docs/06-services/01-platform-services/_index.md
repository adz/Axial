---
title: Platform Services
description: Runtime clock, random and GUID values, observation sinks, and explicit environment-variable authority.
---

Clock, random numbers and GUIDs come from the runtime, not the application environment. The runtime starts with
live implementations; a scoped override applies to its flow and forked descendants, then restores the outer value.
Application authority such as files, HTTP, process execution and environment variables stays explicit in `'env`.

```fsharp
open System
open Axial
open Axial.PlatformService

let isExpired (expiry: DateTimeOffset) : Flow<'env, Never, bool> =
    Clock.now |> Flow.map (fun now -> now >= expiry)
```

| Runtime value | Reader | Scoped override |
| --- | --- | --- |
| `IClock` | `Clock.now`, `Clock.elapsed`, timed Flow operations | `Flow.withClock` |
| `IRandom` | `Random.next`, `nextInt`, `nextDouble`, `bytes` | `Flow.withRandom` |
| `IGuid` | `Guid.newGuid` | `Flow.withGuid` |

`IClock`, `IRandom` and `IGuid` are defined in `Axial`; their reader modules and implementations live in
`Axial.PlatformService`. There is no general runtime service registry. An environment remains an ordinary
application record, and `Flow.envWith` selects its fields.

## Observation slots

`Log.info` and the other logging helpers write to the current `ILogSink`. The default writes Information and above
to stderr, leaving stdout available for application output. `Flow.withLogSink` replaces the sink for a subtree;
`Flow.mapLogSink` decorates the sink already in force. `Flow.withTelemetry` similarly installs the two write-only
`ITelemetry` callbacks for annotations and typed attributes. These slots inherit through forks too.

## Deterministic runs

```fsharp transcript
> open System;;
> open Axial;;
> open Axial.PlatformService;;
> (Clock.now : Flow<unit, Never, DateTimeOffset>) |> Flow.map _.Year |> Flow.runTest ();;
val it: Exit<int,Never> = Success 1970

> (Random.nextInt 1 10 : Flow<unit, Never, int>) |> Flow.withRandom (Random.fromValue 7) |> Flow.runTest ();;
val it: Exit<int,Never> = Success 7
```

`Flow.runTest env` creates a fresh `TestPlatform`: an epoch `ManualClock`, seeded random, sequential GUIDs and a
recording log. `Flow.runTestWith platform env` exposes those fakes for assertions. Neither runner advances time
automatically. Timed tests fork, await a registration signal, explicitly advance, then join; see [clock](clock.html).
The test runners are .NET APIs; Fable supports `ManualClock` and the same scoped per-service overrides.

Environment variables are different: they read or change external state. Supply `IHasEnvironmentVariables` in the
application environment and use `EnvironmentVariables.fromPairs` in tests. Cryptographic randomness belongs inside
an explicit capability using `System.Security.Cryptography`, not the runtime's ordinary random generator.

## In this section

1. [Clock](clock.html): timestamps, durations and explicit manual stepping.
2. [Logging](logging.html): level gates, metadata, sink composition and host adapters.
3. [Randomness and GUIDs](random-and-guid.html): fixed, seeded and scripted values.
4. [Environment variables](environment-variables.html): explicit authority and typed configuration errors.
