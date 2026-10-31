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
type RegionEnv = { Region: string }

let describeRegion : Flow<RegionEnv, Never, string> =
    flow {
        let! now = Clock.now
        let! region = Flow.envWith _.Region
        return $"{region} at {now.Year}"
    }
```

```fsharp run
let env = { Region = "eu" }
describeRegion |> Flow.withClock (Clock.fromValue (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))) |> Flow.run env |> shouldEqual (Exit.Success "eu at 2026")
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

type AppEnv = { Region: string; Connection: Connection }
type AppError = QueryFailed of string

let connectionLayer (closed: ResizeArray<string>) : Layer<unit, Never, Connection> =
    Layer.acquireRelease
        (Layer.succeed (Connection("orders-db", closed)))
        (fun connection ->
            Flow.delay (fun () ->
                connection.Close()
                Flow.ok ()))

let runtime (closed: ResizeArray<string>) : Layer<unit, Never, AppEnv> =
    Layer.merge (Layer.succeed "eu") (connectionLayer closed)
    |> Layer.map (fun (region, connection) -> { Region = region; Connection = connection })

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

## Two provisioning shapes

**Typed startup failure.** A layer can validate configuration before constructing application services:

```fsharp
let connectionString : Layer<IHasEnvironmentVariables, EnvironmentVariableError, string> =
    Layer.fromFlow (EnvironmentVariable.get "CONNECTION_STRING")
```

The error channel is `EnvironmentVariableError`, not `Never`. `Layer.provide` runs the downstream workflow only
after provisioning succeeds, releases resources acquired by earlier steps when a later step fails, and lets
independent steps build in parallel with `merge`. A direct factory returning `Result` can model the error too;
the layer additionally owns acquisition and release.

**A service built from another service.** An HTTP adapter needs an application-owned client and an explicit clock
at its live boundary:

```fsharp
let httpLayer : Layer<System.Net.Http.HttpClient, Never, Axial.HttpClient.IHttp> =
    Layer.envWith (Axial.HttpClient.Http.live Clock.live)
```

The layer consumes the client and constructs an explicit HTTP capability. It does not install a runtime clock or
log sink. Runtime overrides belong around the flow (`Flow.withClock`, `Flow.withLogSink`), not in an environment
layer. The same adaptation can be performed directly when a host constructs its record.

An already-built value that cannot fail needs no provisioning. `Layer.succeed` wraps it:

```fsharp
Layer.succeed Console.live
```

Console, FileSystem, HttpClient and Process are all of this shape, which is why none of them needs a layer to provide it.

## Scopes work without layers

Scopes and `acquireRelease` do not need a layer. See
[scopes and resources](/scopes/index.html).

## In this section

1. [Layers](layers.html): construction, composition, and provisioning failure.
2. [Tutorial](tutorial.html): the same material worked end to end.
