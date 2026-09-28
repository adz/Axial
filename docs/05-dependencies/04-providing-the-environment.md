---
title: Providing the Environment
description: Building the environment value at a host boundary, and the three ways to do it.
---

# Providing the Environment

Everything so far has been about *reading* the environment. This page is about producing the value in the first
place, once at startup and again in each test.

There are three ways. Prefer them in this order.

## 1. Construct it

Build the record and hand it over. Nothing else is involved:

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
type IUserStore =
    abstract Count: unit -> int

type AppEnv = { Users: IUserStore; Clock: IClock }

let summary : Flow<AppEnv, Never, string> =
    flow {
        let! users = Flow.envWith _.Users
        let! clock = Flow.envWith _.Clock
        let today = clock.UtcNow().ToString "yyyy-MM-dd"
        return $"{users.Count()} users on {today}"
    }
```

Production builds the record from the live services, for example `{ Users = SqlUserStore(connectionString); Clock =
Clock.live }`. A test builds it from fixed ones:

```fsharp run
let fixedEnv =
    { Users = { new IUserStore with member _.Count() = 3 }
      Clock = Clock.fromValue (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)) }

summary |> Flow.run fixedEnv |> shouldEqual (Exit.Success "3 users on 2026-01-01")
```

Most applications need nothing more. The wiring is a value literal rather than a resolution process, so a reader can
see where every service comes from.

For the operational services, `Axial.PlatformService` ships a ready-made bundle so you do not have to name all five:

```fsharp transcript
> open System;;
> open Axial.PlatformService;;
> let runtime () = { BaseRuntime.liveValue with Clock = Clock.fromValue (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)) };;
> (Clock.now : Flow<BaseRuntime, Never, DateTimeOffset>) |> Flow.map _.Year |> Flow.run (runtime ());;
val it: Exit<int,Never> = Success 2026
```

`BaseRuntime` groups `IClock`, `ILog`, `IRandom`, `IGuid`, and `IEnvironmentVariables`, and implements one contract
per service, so helpers like `Clock.now` and `EnvironmentVariable.get` work against it directly. Embedding it
alongside your own services takes one interface member per service, delegating to wherever `BaseRuntime` ends up
living in your record; see [Tutorial: Composing Built-in Services](/services/existing-services.html) for the full
pattern.

## 2. Take it from a host container

.NET hosts already have an `IServiceProvider`. Use it to *build* the environment, then leave it behind:

```fsharp
type IOrderQueue =
    abstract Flush: unit -> int

let flushOrders : Flow<IServiceProvider, unit, int> =
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

flushOrders |> Flow.run withQueue |> shouldEqual (Exit.Success 5)

match flushOrders |> Flow.run (provider []) with
| Exit.Failure(Cause.Die _) -> ()
| other -> failwithf "expected a defect, got %A" other
```

`ServiceProvider.get` treats a missing registration as a **defect**, not a typed error, because an unregistered
service is a configuration bug rather than something a workflow should handle.

The rule is one line: **use `IServiceProvider` to build the world; do not make every business workflow depend on it.**
A workflow typed `Flow<IServiceProvider, _, _>` can reach anything, which is exactly the property the environment
channel exists to remove. Convert at the edge and let the rest of the application name what it needs.

## 3. Provision it with a layer

When building the environment is itself effectful (it can fail with a typed startup error, needs a resource released
later, or must await something), construction becomes a workflow of its own. [Layers](/layers/index.html) are those
workflows. They live in a separate package because most applications never need them.

The signal is in the type. `Layer<IServiceProvider, BaseRuntimeError, BaseRuntime>` says: consumes a provider, may
fail with a typed startup error, produces a runtime. `Axial.PlatformService` ships exactly that as
`BaseRuntime.fromServiceProvider`, which turns dynamic registrations into an explicit `BaseRuntime` and reports
anything missing as `BaseRuntimeError.MissingService` **before** the first workflow runs.

```fsharp
let currentYear : Flow<BaseRuntime, BaseRuntimeError, int> = Clock.now |> Flow.map _.Year

let fromHost : Flow<IServiceProvider, BaseRuntimeError, int> =
    currentYear |> Layer.provide BaseRuntime.fromServiceProvider
```

An empty provider fails before `currentYear` runs, with the first service it could not find:

```fsharp run
fromHost |> Flow.run (provider []) |> shouldEqual (Exit.Failure(Cause.Fail(BaseRuntimeError.MissingService "IClock")))
```

## Choosing

| Situation | Use |
| --- | --- |
| You can build the value | Construct it and call `Flow.run` |
| A host container owns the implementations | `ServiceProvider.get` at the edge |
| Construction can fail, block, or acquire | A [layer](/layers/index.html) |

Tests almost always want the first row, whatever production uses.
