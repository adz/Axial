---
title: Providing the Environment
description: Building the environment value at a host boundary, and the three ways to do it.
---

# Providing the Environment

Build the environment at startup and in each test. Prefer these approaches in order.

## 1. Construct it

Build the record and pass it to `Flow.run`:

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
type IUserStore =
    abstract Count: unit -> int

type AppEnv = { Users: IUserStore }

let summary : Flow<AppEnv, Never, string> =
    flow {
        let! users = Flow.envWith _.Users
        let! now = Clock.now
        let today = now.ToString "yyyy-MM-dd"
        return $"{users.Count()} users on {today}"
    }
```

Production builds the record from the live services; `SqlUserStore` is a placeholder for your own store implementation, not a type Axial ships. For example: `{ Users = SqlUserStore(connectionString) }`. A test builds it from fixed ones:

```fsharp run
let fixedEnv =
    { Users = { new IUserStore with member _.Count() = 3 } }

summary |> Flow.withClock (Clock.fromValue (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))) |> Flow.run fixedEnv |> shouldEqual (Exit.Success "3 users on 2026-01-01")
```

The record literal shows where each service comes from.

Clock, random and GUID values are runtime services, not authority in this record. Install test values with
`Flow.withClock`, `Flow.withRandom` and `Flow.withGuid`. Log and telemetry destinations use `Flow.withLogSink`
and `Flow.withTelemetry`. These scoped overrides inherit through forks and restore the outer values on exit.
Environment-variable reads still require an explicit `IHasEnvironmentVariables` dependency; see
[Tutorial: Composing Built-in Services](/services/existing-services.html).

## 2. Take it from a host container

.NET hosts already have an `IServiceProvider`. Use it to *build* the environment, then leave it behind.
Resolving inside the workflow is the escape hatch, not the default shape:

```fsharp
type IOrderQueue =
    abstract Flush: unit -> int

let flushOrdersFromProvider : Flow<IServiceProvider, unit, int> =
    flow {
        let! orders = ServiceProvider.get<IOrderQueue, _, _> ()
        return orders.Flush()
    }
```

A host's container supplies the `IServiceProvider`. The examples use a small stand-in:

```fsharp
let provider (services: (Type * obj) list) =
    { new IServiceProvider with
        member _.GetService serviceType =
            services |> List.tryFind (fst >> (=) serviceType) |> Option.map snd |> Option.defaultValue null }
```

```fsharp run
let withQueue = provider [ typeof<IOrderQueue>, box { new IOrderQueue with member _.Flush() = 5 } ]

flushOrdersFromProvider |> Flow.run withQueue |> shouldEqual (Exit.Success 5)

match flushOrdersFromProvider |> Flow.run (provider []) with
| Exit.Failure(Cause.Die _) -> ()
| other -> failwithf "expected a defect, got %A" other
```

`ServiceProvider.get` treats a missing registration as a **defect**, not a typed error, because an unregistered
service is a configuration bug rather than something a workflow should handle.

The rule is one line: **use `IServiceProvider` to build the world; do not make every business workflow depend on it.**
A workflow typed `Flow<IServiceProvider, _, _>` can reach anything, which is exactly the property the environment
channel exists to remove. Convert at the edge and let the rest of the application name what it needs. At the
composition root, resolve the dependencies once and hand the application an ordinary record:

```fsharp
type OrderEnv = { Orders: IOrderQueue }

let buildOrderEnv : Flow<IServiceProvider, unit, OrderEnv> =
    flow {
        let! orders = ServiceProvider.get<IOrderQueue, _, _> ()
        return { Orders = orders }
    }

let flushOrders : Flow<OrderEnv, unit, int> =
    flow {
        let! orders = Flow.envWith _.Orders
        return orders.Flush()
    }
```

```fsharp run
let appEnv =
    match buildOrderEnv |> Flow.run (provider [ typeof<IOrderQueue>, box { new IOrderQueue with member _.Flush() = 5 } ]) with
    | Exit.Success env -> env
    | Exit.Failure cause -> failwithf "startup failed: %A" cause

flushOrders |> Flow.run appEnv |> shouldEqual (Exit.Success 5)
```

With this shape, the container lookup happens once in `buildOrderEnv`; `flushOrders` and the rest of the application name only the record.

## 3. Provision it with a layer

When building the environment is itself effectful (it can fail with a typed startup error, needs a resource released
later, or must await something), construction becomes a workflow of its own. [Layers](/layers/index.html) are the core
combinators for those workflows.

A layer acquires its resources inside the scope `Layer.provide` creates, so they are released when the provided flow
finishes, fails, or is interrupted. The [layers guide](/layers/layers.html#cleanup) opens and closes a repository
around the workflow, and the [resources guide](/the-flow-type/resources.html) covers scope-based cleanup directly.

Choose an explicit typed startup error when a missing registration is part of provisioning's contract:

```fsharp
let orderLayer : Layer<IServiceProvider, string, OrderEnv> =
    Layer.fromFlow (
        Flow.envWith (fun (provider: IServiceProvider) ->
            match provider.GetService typeof<IOrderQueue> with
            | :? IOrderQueue as queue -> Ok { Orders = queue }
            | _ -> Error "Order queue was not registered")
        |> Flow.bind Flow.fromResult)

let fromHost : Flow<IServiceProvider, string, int> =
    Flow.envWith (fun (env: OrderEnv) -> env.Orders.Flush()) |> Layer.provide orderLayer
```

An empty provider fails before the application runs:

```fsharp run
fromHost |> Flow.run (provider []) |> shouldEqual (Exit.Failure(Cause.Fail "Order queue was not registered"))
```

The layer consumes the provider and produces only the application's authority record. Runtime values need no
container bundle; hosts install their clock/log overrides separately at the execution boundary.

## Choosing

| Situation | Use |
| --- | --- |
| You can build the value | Construct it and call `Flow.run` |
| A host container owns the implementations | `ServiceProvider.get` at the edge |
| Construction can fail, block, or acquire | A [layer](/layers/index.html) |

Tests almost always want the first row, whatever production uses.
