---
title: Supplying Platform Services
linkTitle: Supplying Platform Services
description: Install runtime values and observation sinks at an application boundary.
---

# Supplying Platform Services

A root Flow starts with live clock/random/GUID implementations and an Information+ stderr log sink. No platform
bundle is required in `'env`. Keep authority such as configuration, HTTP, files and process execution in
an application record; override runtime values around the flow itself.

```fsharp prepare
open System
open Axial
open Axial.PlatformService
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
open Axial.Configuration
open Reified
open Reified.SchemaDSL

type StartupSettings = { Port: int }

let startupSchema () =
    schema<StartupSettings> {
        field (fun (settings: StartupSettings) -> settings.Port)
        construct (fun port -> { Port = port })
    }

let startup : Flow<unit, ConfigError list, int> =
    Config.load (startupSchema ()) [ ConfigSource.environmentOf None [ "PORT", "8080" ] ]
    |> Flow.bind (fun settings ->
        flow {
            let! now = Clock.now
            do! Log.info ("Starting port " + string settings.Port + " in " + string now.Year)
            return settings.Port
        })
```

```fsharp run
let log = Log.memory ()
startup
|> Flow.withClock (Clock.fromValue (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)))
|> Flow.withRandom (Random.seeded 42)
|> Flow.withGuid (Guid.fromValue System.Guid.Empty)
|> Flow.withLogSink log
|> Flow.run ()
|> shouldEqual (Exit.Success 8080)
log.Entries |> Seq.map _.Message |> Seq.toList |> shouldEqual ["Starting port 8080 in 2026"]
```

Overrides apply to a subtree and its forks; the outer values are restored afterwards. `Flow.mapLogSink` decorates
an installed sink instead of replacing it. `Flow.withTelemetry` installs annotation and typed-attribute callbacks;
Activity and JavaScript OpenTelemetry adapters compose through them rather than replacing them.

## Host logging defaults

| Host | Installed destination |
| --- | --- |
| Generic Host | Microsoft logging, category `Axial` |
| ASP.NET Core flowEndpoint | RequestServices' Microsoft logging factory, category `Axial.Endpoint` |
| GenHTTP | Runtime default unless `EndpointOptions.LogSink` is supplied |
| Node | `NodeLogging.sink LogLevel.Information`, through console.error on stderr |
| Browser | `BrowserLogging.sink LogLevel.Information`, mapped to console.debug/info/warn/error |

`HostedAppOptions.LogSink` and ASP.NET `EndpointOptions.LogSink` accept an optional factory callback from
`ILoggerFactory` to `ILogSink`. GenHTTP, Node and Browser options accept an optional sink directly. `None` leaves
the runtime default in force; a supplied `LogSink.none` silences logs. An inner `Flow.withLogSink` always wins.
Microsoft logging scopes include annotations and named FiberLocals; empty entries add no empty scope.
`FiberLogging.observe` remains opt-in and separate from message logging.

The default `flowEndpoint` functions retain their existing argument order. Use `flowEndpointWithOptions` for an
explicit endpoint policy. Node offers `startWithOptions` / `runWithOptions`; Browser offers `mountWithOptions` /
`startWithSignalOptions`. See [.NET hosting](dotnet.html), [Node](node.html) and [application lifecycle](application-lifecycle.html).

## Tests and authority

`Flow.runTest env` creates a fresh deterministic `TestPlatform`. `Flow.runTestWith platform env` exposes its
manual clock, recording log, random and GUID implementations. Neither advances time automatically: fork a timed
flow, await its sleeper-registration signal, call `TestClock.advance`, then join. `advanceAll` stops at the latest
deadline registered at entry rather than following a repeating timer forever.

These runners are .NET-only. Fable uses the same `ManualClock` and scoped overrides; after advancing, await the
released promise/fiber before a dependent advance.

A container should resolve application authority once and construct the environment. It is not a runtime-service
registry. See [providing the environment](/dependencies/providing-the-environment.html) and
[service details](/services/platform-services/index.html). Cryptographic randomness belongs in explicit capability
code using System.Security.Cryptography, not the runtime random generator.
