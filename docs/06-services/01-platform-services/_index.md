---
title: Platform Services
description: Runtime clock, random, GUID, culture and time-zone values, observation sinks, and explicit environment-variable authority.
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

`IClock`, `IRandom`, `IGuid` and the platform modules are defined in `Axial`. Their reader modules keep the
`Axial.PlatformService` namespace for 0.12, but the implementation ships in the `Axial` package. The
`Axial.PlatformService` package now holds only environment-variable services. There is no general runtime service
registry. An environment remains an ordinary application record, and `Flow.envWith` selects its fields.

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

## Culture and time zone

`Culture` and `TimeZone` are plain sealed values, not runtime-context fields. Read a machine default as an explicit
effect with `Culture.system` or `TimeZone.local`, keep the value in the application model, and pass it to pure
functions. `Flow.runTest` supplies the invariant culture and UTC. `Culture.setProcessDefault` sets the .NET process
culture once at startup; it is a no-op on Fable.

`Format.number`, `Format.currency`, `Format.date` and `Format.time` take the culture first. `TimeZone.toLocal zone
instant` converts a UTC instant to the zone's wall clock for presentation. On .NET formatting uses `CultureInfo`; on
Fable it uses `Intl`. Both use the instant's wall clock, not the machine's local time.

The targets agree on the common tested output but not on every format. Known differences:

- `Format.number` uses the .NET `"N2"` pattern and `Intl.NumberFormat` with two fractional digits. They agree for
  de-DE and en-US in the tests; other .NET number patterns are not exposed.
- `Format.currency` on .NET uses the culture's currency symbol and pattern. On Fable it derives a currency from the
  culture's region and falls back to USD for unmapped regions. The invariant culture therefore renders `¤1,234.50` on
  .NET and `$1,234.50` on Fable.
- `Format.date` and `Format.time` use the .NET short date/time patterns (`"d"` and `"t"`) with `CultureInfo`, while
  Fable builds numeric `Intl.DateTimeFormat` fields. Zero padding and separators can differ: de-DE date is
  `01.06.2025` on .NET and commonly `1.6.2025` on Fable. en-US time can differ in the non-breaking space before `PM`.
- The invariant culture uses en-US as the Fable `Intl` baseline because `Intl` has no invariant locale. Use an explicit
  culture for presentation and do not compare invariant `Format.currency`/date/time strings across targets.

Configuration is different: environment variables, arguments and files read external state. Supply `IHasConfig` in
the application environment, or load once with `Config.load`, and build sources from pairs in tests. Cryptographic randomness belongs inside
an explicit capability using `System.Security.Cryptography`, not the runtime's ordinary random generator.

## In this section

1. [Clock](clock.html): timestamps, durations and explicit manual stepping.
2. [Logging](logging.html): level gates, metadata, sink composition and host adapters.
3. [Randomness and GUIDs](random-and-guid.html): fixed, seeded and scripted values.
4. [Environment variables](environment-variables.html): explicit authority and typed configuration errors.
