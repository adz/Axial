---
title: Layers
description: Reusable provisioning for environments that need flow capabilities to build.
---

A layer builds an environment, and building it may itself need flow capabilities: awaiting a
connection, reading configuration, failing with a typed startup error, or acquiring something that
must be released again. `Axial.Layers` is a separate package because most applications never need
that.

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

That covers most applications, needs no package beyond the services themselves, and is what
[dependencies](/dependencies/index.html) documents.

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
        (fun connection _ ->
            connection.Close()
            Task.CompletedTask)

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

Axial's own packages contain both of the cases that justify a layer.

**Provisioning that can fail with a typed error.** `Axial.PlatformService` builds the five standard services from a
host container:

```fsharp no-check reason="Excerpted from the Axial.PlatformService source"
let servicesFromServiceProvider
    : Layer<IServiceProvider, BaseRuntimeError, IClock * ILog * IRandom * IGuid * IEnvironmentVariables> =
    Layer.fromValueTask (fun (provider, _) _ ->
        task {
            match tryService<IClock> provider, tryService<ILog> provider, tryService<IRandom> provider,
                  tryService<IGuid> provider, tryService<IEnvironmentVariables> provider with
            | Ok clock, Ok log, Ok random, Ok guid, Ok environmentVariables ->
                return Exit.Success(clock, log, random, guid, environmentVariables)
            | Error name, _, _, _, _ | _, Error name, _, _, _ | _, _, Error name, _, _
            | _, _, _, Error name, _ | _, _, _, _, Error name ->
                return Exit.Failure(Cause.Fail(BaseRuntimeError.MissingService name))
        })
```

Read the error channel: `BaseRuntimeError`, not `Never`. **Construction itself can fail**, and it fails with a typed
error naming the missing service. A record cannot express that; you would throw, or return an option and push the
problem onto every caller. `Layer.provide` surfaces it as a typed startup failure before any workflow runs.

**A service built from another service.** `Axial.Hosting` turns what the host container has into what workflows
need:

```fsharp no-check reason="Excerpted from the Axial.Hosting source"
let layer (categoryName: string) : Layer<ILoggerFactory, Never, ILog> =
    Layer.fromValueTask (fun (loggerFactory, _) _ ->
        ValueTask<Exit<ILog, Never>>(Exit.Success(fromFactory categoryName loggerFactory)))
```

The type says it: consumes an `ILoggerFactory`, produces an `ILog`. The factory does not exist until the host starts,
so there is no record field to put it in. The layer does the conversion.

Compare with the case that does **not** need a layer. Wrapping a value that is already built and cannot fail is
`Layer.succeed`, which provisions nothing:

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
