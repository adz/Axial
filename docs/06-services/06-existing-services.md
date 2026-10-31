---
title: "Tutorial: Composing Built-in Services"
description: Combine explicit application authority with scoped runtime values and observation sinks.
---

# Tutorial: Composing Built-in Services

An application environment holds dependencies with authority: configuration, repositories, HTTP clients, files
and process execution. Clock, ordinary random values and GUIDs come from the runtime. Logging and telemetry are
write-only observation slots. None of those runtime slots needs forwarding interfaces on your record.

```fsharp prepare
open System
open System.Threading.Tasks
open Axial
open Axial.PlatformService
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

## Declare application authority

An application record carries authority and exposes only what its workflows need:

```fsharp
type IAppSettings =
    abstract Mode: string

type AppEnv =
    { Settings: IAppSettings }

let loadMode : Flow<AppEnv, Never, string> =
    flow {
        let! now = Clock.utcDateTime
        let! mode = Flow.envWith (fun env -> env.Settings.Mode)
        do! Log.info $"[{now:O}] starting in mode {mode}"
        return mode
    }
```

Only the settings projection constrains the application environment. The clock and log destination can be
controlled without changing `AppEnv` or the workflow. Loading configuration into `IAppSettings` at the edge is
covered in the [configuration guide](/configuration/configuration.html).

## Add application dependencies

An ordinary projection needs no service interface:

```fsharp
type IOrderRepository =
    abstract Count: unit -> int

type ShopEnv =
    { Orders: IOrderRepository }

let countOrders : Flow<ShopEnv, Never, int> = Flow.envWith (fun env -> env.Orders.Count())
```

Use `Flow.envWith _.Orders` when the workflow belongs to this record. Add a named authority contract only when
helpers should work across environment shapes; see [custom services](/advanced/custom-services.html).

## Run at the edge

```fsharp
let runLive (settings: IAppSettings) : Task<Exit<string, Never>> =
    loadMode
    |> Flow.startTask { Settings = settings }
```

The host constructs the application authority at the edge and supplies it in the environment. The runtime supplies
live clock/random/GUID values and an Information+ stderr log sink. Generic Host and HTTP host adapters can install
their own sinks; an inner scoped override still wins.

## Test without global state

A workflow that reads runtime values can be tested without global state by overriding the scoped values:

```fsharp run
let testEnv =
    { Settings = { new IAppSettings with member _.Mode = "diagnostic" } }

let log = Log.memory ()
loadMode
|> Flow.withClock (Clock.fromValue (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)))
|> Flow.withLogSink log
|> Flow.run testEnv
|> shouldEqual (Exit.Success "diagnostic")
log.Entries |> Seq.map _.Message |> Seq.toList
|> shouldEqual ["[2026-01-01T00:00:00.0000000Z] starting in mode diagnostic"]
```

`Flow.withRandom` and `Flow.withGuid` follow the same scope rule as `withClock` and `withLogSink`; descendants
inherit them and outer values are restored afterwards. `Flow.runTest env` installs a fresh deterministic
`TestPlatform`, including a recording log, without moving authority out of the environment.

Use [layers](/layers/index.html) when constructing the application environment acquires resources or can fail.
Use runtime overrides for values and observation slots, not as a general-purpose dependency registry.
