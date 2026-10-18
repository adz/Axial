---
title: "Tutorial: Composing Built-in Services"
description: Embed BaseRuntime and your own dependencies in one application environment.
---

# Tutorial: Composing Built-in Services

An application can combine services constrained by `IHasX` interfaces with its own dependencies in one environment.

## The problem

[`BaseRuntime`](platform-services/index.html) bundles the five platform services and already implements
`IHasClock`, `IHasLog`, `IHasRandom`, `IHasGuid`, and `IHasEnvironmentVariables`. Embedding it as a field of your
own record does not carry those interface implementations with it: F# has no mechanism for one type to forward
another type's interfaces automatically. Your own environment record has to state, once per service, where that
service lives:

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
open Axial.PlatformService

type AppEnv =
    { Runtime: BaseRuntime }

    interface IHasClock with
        member this.Clock = this.Runtime.Clock

    interface IHasLog with
        member this.Log = this.Runtime.Log

    interface IHasEnvironmentVariables with
        member this.EnvironmentVariables = this.Runtime.EnvironmentVariables
```

`member this.Clock = this.Runtime.Clock` delegates the contract to the `Runtime` field. Declare interface members for the services the application uses, once on the environment record.

## Use the services

The workflow uses the same service APIs whether `Runtime` holds them or they are top-level fields:

```fsharp
let loadMode : Flow<AppEnv, EnvironmentVariableError, string> =
    flow {
        let! now = Clock.utcDateTime
        let! mode = EnvironmentVariable.get "APP_MODE"
        do! Log.info $"[{now:O}] starting in mode {mode}"
        return mode
    }
```

`loadMode` does not know that `Clock`, `Log`, and `EnvironmentVariable` all come from the same `Runtime` field; it
only knows the three interfaces. Swap `AppEnv` for any other type that implements them and the
workflow is unchanged.

## Add your own dependencies alongside it

`AppEnv` is an ordinary record, so extending it with an application-specific dependency is the same pattern as
[the app record tutorial](/dependencies/tutorials/app-record.html): add a field, add an interface if other
helpers should depend on the contract rather than the field name directly:

```fsharp
type IOrderRepository =
    abstract Count: unit -> int

type ShopEnv =
    { Runtime: BaseRuntime
      Orders: IOrderRepository }

    interface IHasClock with
        member this.Clock = this.Runtime.Clock

    interface IHasLog with
        member this.Log = this.Runtime.Log

    interface IHasEnvironmentVariables with
        member this.EnvironmentVariables = this.Runtime.EnvironmentVariables
```

A workflow that reads `Orders` directly (`Flow.envWith _.Orders`) is coupled to this record's field name. If you
want `Orders` reusable behind a named contract instead, the way `Clock` and `Log` are, see
[Tutorial: Creating Reusable Services](/advanced/custom-services.html).

## Run it

In production the environment holds the live services:

```fsharp
let runLive () : Task<Exit<string, EnvironmentVariableError>> =
    loadMode |> Flow.startTask { Runtime = BaseRuntime.liveValue }
```

## Test it

Tests can substitute services through `BaseRuntime`'s fields. `BaseRuntime`'s fields each accept the fixed value shown in
[deterministic implementations](platform-services/index.html#deterministic-implementations), so a test builds the
whole environment as one value literal, with no interface to reimplement:

```fsharp run
let gate = obj()
let mutable logged = []

let testEnv =
    { Runtime =
        { Clock = Clock.fromValue (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
          Log = Log.fromSink (fun _ message -> lock gate (fun () -> logged <- message :: logged))
          Random = Random.fromValue 7
          Guid = Guid.fromValue (Guid.Parse "00000000-0000-0000-0000-000000000001")
          EnvironmentVariables = EnvironmentVariables.fromPairs [ "APP_MODE", "diagnostic" ] } }

loadMode |> Flow.run testEnv |> shouldEqual (Exit.Success "diagnostic")
lock gate (fun () -> List.rev logged) |> shouldEqual [ "[2026-01-01T00:00:00.0000000Z] starting in mode diagnostic" ]
```

See [Tutorial: Creating Reusable Services](/advanced/custom-services.html) for an application-specific service contract, or [Layers](/layers/tutorial.html) when building the environment itself becomes effectful.
