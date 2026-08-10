---
title: Providing the Environment
description: Building the environment value at a host boundary, and the three ways to do it.
---

# Providing the Environment

Everything so far has been about *reading* the environment. This page is about producing the value in the first
place — the step that happens once, at startup, and again in each test.

There are three ways, in the order you should reach for them.

## 1. Construct it

Build the record and hand it over. Nothing else is involved:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let live =
    { Users = SqlUserStore(connectionString)
      Audit = FileAuditLog(logPath)
      Clock = Clock.live }

let exit = program |> Flow.run live
```

This is the right answer far more often than it looks. It is also the fastest thing to read six months later, because
the wiring is a value literal rather than a resolution process.

For the operational services, `Axial.PlatformService` ships a ready-made bundle so you do not have to name all five:

```fsharp no-check reason="Shown independently; surrounding application context is intentionally omitted"
let result = Clock.now |> Flow.run BaseRuntime.liveValue
```

`BaseRuntime` groups `IClock`, `ILog`, `IRandom`, `IGuid`, and `IEnvironmentVariables`, and implements one contract
per service, so helpers like `Clock.now` and `EnvironmentVariable.get` work against it directly. Embedding it
alongside your own services takes one interface member per service, delegating to wherever `BaseRuntime` ends up
living in your record — see [Tutorial: Composing Built-in Services](/services/existing-services.html) for the full
pattern.

## 2. Take it from a host container

.NET hosts already have an `IServiceProvider`. Use it to *build* the environment, then leave it behind:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let handler : Flow<IServiceProvider, unit, unit> =
    flow {
        let! orders = ServiceProvider.get<IOrderRepository, _, _>()
        do! orders.Flush()
    }
```

`ServiceProvider.get` treats a missing registration as a **defect**, not a typed error, because an unregistered
service is a configuration bug rather than something a workflow should handle.

The rule is one line: **use `IServiceProvider` to build the world; do not make every business workflow depend on it.**
A workflow typed `Flow<IServiceProvider, _, _>` can reach anything, which is exactly the property the environment
channel exists to remove. Convert at the edge and let the rest of the application name what it needs.

## 3. Provision it with a layer

When building the environment is *itself* effectful — it can fail with a typed startup error, needs a resource
released later, or must await something — construction becomes a workflow of its own. That is what
[layers](/layers/index.html) are, and they live in a separate package because most applications never reach this
case.

The signal is in the type. `Layer<IServiceProvider, BaseRuntimeError, BaseRuntime>` says: consumes a provider, may
fail with a typed startup error, produces a runtime. `Axial.PlatformService` ships exactly that as
`BaseRuntime.fromServiceProvider`, which turns dynamic registrations into an explicit `BaseRuntime` and reports
anything missing as `BaseRuntimeError.MissingService` **before** the first workflow runs.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let runnable = workflow |> Layer.provide BaseRuntime.fromServiceProvider
```

## Choosing

| Situation | Use |
| --- | --- |
| You can build the value | Construct it and call `Flow.run` |
| A host container owns the implementations | `ServiceProvider.get` at the edge |
| Construction can fail, block, or acquire | A [layer](/layers/index.html) |

Tests almost always want the first row, whatever production uses.
