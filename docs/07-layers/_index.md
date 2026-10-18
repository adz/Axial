---
title: Layers
description: Reusable provisioning for environments that need flow capabilities to build.
---

A layer builds an environment using flow capabilities when construction must await a connection, read configuration, fail with a typed startup error, or acquire a resource that must be released. Most applications do not need the separate `Axial.Layers` package.

**Use a record when you can.** Construct the environment directly and hand it to the workflow:

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
type RegionEnv = { Clock: IClock; Region: string }

let describeRegion : Flow<RegionEnv, Never, string> =
    flow {
        let! clock = Flow.envWith _.Clock
        let! region = Flow.envWith _.Region
        return $"{region} at {clock.UtcNow().Year}"
    }
```

```fsharp run
let env = { Clock = Clock.fromValue (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)); Region = "eu" }
describeRegion |> Flow.run env |> shouldEqual (Exit.Success "eu at 2026")
```

Direct construction needs no package beyond the services themselves. See [dependencies](/dependencies/index.html).

Use a layer when construction is itself effectful:

- provisioning can fail, and the failure should be a typed startup error rather than an exception
- a service must be acquired and released, and its lifetime is the runtime's
- independent parts of the environment should be built in parallel
- a service needs another service in order to be constructed

```fsharp
type Connection(name: string, closed: ResizeArray<string>) =
    member _.Name = name
    member _.Close() = closed.Add name

type AppEnv = { Clock: IClock; Connection: Connection }
type AppError = QueryFailed of string

let connectionLayer (closed: ResizeArray<string>) : Layer<unit, Never, Connection> =
    Layer.acquireRelease
        (Layer.succeed (Connection("orders-db", closed)))
        (fun connection ->
            Flow.delay (fun () ->
                connection.Close()
                Flow.ok ()))

let runtime (closed: ResizeArray<string>) : Layer<unit, Never, AppEnv> =
    Layer.merge Clock.layer (connectionLayer closed)
    |> Layer.map (fun (clock, connection) -> { Clock = clock; Connection = connection })

let query : Flow<AppEnv, AppError, string> =
    Flow.envWith (fun env -> env.Connection.Name)

let program (closed: ResizeArray<string>) : Flow<unit, AppError, string> =
    Layer.provide (Layer.widenError (runtime closed)) query
```

`runtime` cannot fail, so its error type is `Never`; `Layer.widenError` lets it provide a workflow that fails with
`AppError`. The connection is closed when `program` ends:

```fsharp run
let closed = ResizeArray<string>()
program closed |> Flow.run () |> shouldEqual (Exit.Success "orders-db")
List.ofSeq closed |> shouldEqual [ "orders-db" ]
```

`Layer.provide` is the boundary: it opens a scope, builds the layer inside it, runs the downstream
flow with the result, and closes the scope afterwards whether the flow succeeds, fails, or is
interrupted.

## Two real examples

Axial packages use layers for typed provisioning errors and service construction from other services.

**Provisioning that can fail with a typed error.** `Axial.PlatformService` builds the five standard services from a
host container:

```fsharp no-check reason="Excerpted from the Axial.PlatformService source"
let servicesFromServiceProvider
    : Layer<IServiceProvider, BaseRuntimeError, IClock * ILog * IRandom * IGuid * IEnvironmentVariables> =
    Layer.fromFlow (
        Flow.envWith (fun provider ->
            match tryService<IClock> provider, tryService<ILog> provider, tryService<IRandom> provider,
                  tryService<IGuid> provider, tryService<IEnvironmentVariables> provider with
            | Ok clock, Ok log, Ok random, Ok guid, Ok environmentVariables ->
                Ok(clock, log, random, guid, environmentVariables)
            | Error name, _, _, _, _ | _, Error name, _, _, _ | _, _, Error name, _, _
            | _, _, _, Error name, _ | _, _, _, _, Error name ->
                Error(BaseRuntimeError.MissingService name))
        |> Flow.bind Flow.fromResult
    )
```

The error channel is `BaseRuntimeError`, not `Never`. Construction can fail with a typed error naming the missing service. A record cannot express that; you would throw, or return an option and push the problem onto every caller. `Layer.provide` surfaces it as a typed startup failure before any workflow runs.

**A service built from another service.** `Axial.Hosting` turns what the host container has into what workflows
need:

```fsharp no-check reason="Excerpted from the Axial.Hosting source"
let layer (categoryName: string) : Layer<ILoggerFactory, Never, ILog> =
    Layer.envWith (fromFactory categoryName)
```

The layer consumes an `ILoggerFactory` and produces an `ILog`. The factory does not exist until the host starts, so there is no record field to put it in.

An already-built value that cannot fail needs no provisioning. `Layer.succeed` wraps it:

```fsharp
Layer.succeed Console.live
```

Console, FileSystem, HttpClient and Process are all of this shape, which is why none of them depends on this package.

## Scopes are not part of this package

Scopes and `acquireRelease` are core, and work without layers. See
[scopes and resources](/scopes/index.html).

## In this section

1. [Layers](layers.html): construction, composition, and provisioning failure.
2. [Tutorial](tutorial.html): the same material worked end to end.
