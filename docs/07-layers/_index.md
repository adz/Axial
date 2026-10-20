---
title: Layers
description: Reusable provisioning for environments that need flow capabilities to build.
---

Layers are not required: when every service can be built synchronously, a record built in code is enough. Once building the environment means awaiting a connection, reading configuration, failing with a typed startup error, or acquiring something that must be released (a database pool, a socket), a layer does that work and releases it in reverse order on shutdown. It is a small set of combinators over `Flow` and `Scope` for exactly those needs, and it ships in the core `Axial` package.

**Use a record when you can.** Construct the environment directly and hand it to the workflow:

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
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

Direct construction needs no layer. See [dependencies](/dependencies/index.html).

Use a layer when construction is itself effectful:

- provisioning can fail, and the failure should be a typed startup error rather than an exception
- a service must be acquired and released for the duration of the flow `Layer.provide` runs: acquired during provisioning and released when that flow ends, whether it succeeds, fails, or is interrupted
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

The error channel is `BaseRuntimeError`, not `Never`. Construction can fail with a typed error naming the missing
service. A factory returning `Result<'env, BaseRuntimeError>` could model that typed failure in a record, but a layer
keeps the failure inside the provisioning step: `Layer.provide` runs the workflow only after every step succeeds,
releases resources acquired by earlier steps when one fails, and lets independent steps build in parallel with
`merge`. A missing service surfaces as a typed startup failure before any workflow runs.

**A service built from another service.** `Axial.Hosting` turns what the host container has into what workflows
need:

```fsharp no-check reason="Excerpted from the Axial.Hosting source"
let layer (categoryName: string) : Layer<ILoggerFactory, Never, ILog> =
    Layer.envWith (fromFactory categoryName)
```

The layer consumes the host's `ILoggerFactory` and produces an `ILog` named by the category. The factory arrives only
after the host starts, so `Layer.envWith (fromFactory categoryName)` adapts it at the `provide` boundary. This
demonstrates adapting one dependency into another — not a limitation of records, since a record built after startup
could hold the factory or the resulting logger directly.

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
