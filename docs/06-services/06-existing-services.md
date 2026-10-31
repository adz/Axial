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
open Axial.Configuration
open Axial.PlatformService
open Reified
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

## Declare application authority

Configuration remains explicit because it reads external state:

```fsharp
type AppEnv =
    { Config: IConfig }
    interface IHasConfig with member this.Config = this.Config

let loadMode : Flow<AppEnv, ConfigError, string> =
    flow {
        let! now = Clock.utcDateTime
        let! mode = Config.required "AppMode"
        do! Log.info $"[{now:O}] starting in mode {mode}"
        return mode
    }
```

Only the configuration read constrains the application environment. The clock and log destination can be
controlled without changing `AppEnv` or the workflow.

## Add application dependencies

An ordinary projection needs no service interface:

```fsharp
type IOrderRepository =
    abstract Count: unit -> int

type ShopEnv =
    { Config: IConfig
      Orders: IOrderRepository }
    interface IHasConfig with member this.Config = this.Config

let countOrders : Flow<ShopEnv, Never, int> = Flow.envWith (fun env -> env.Orders.Count())
```

Use `Flow.envWith _.Orders` when the workflow belongs to this record. Add a named authority contract only when
helpers should work across environment shapes; see [custom services](/advanced/custom-services.html).

## Run at the edge

```fsharp
let runLive () : Task<Exit<string, ConfigError>> =
    loadMode
    |> Flow.startTask
        { Config =
            Config.ofData (
                Data.Object
                    [ "AppMode", Data.Text "diagnostic" ]
            ) }
```

The runtime supplies live clock/random/GUID values and an Information+ stderr log sink. Generic Host and HTTP
host adapters can install their own sinks; an inner scoped override still wins.

## Test without global state

```fsharp run
let log = Log.memory ()
let testEnv = { Config = Config.ofData (Data.Object [ "AppMode", Data.Text "diagnostic" ]) }
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
