---
weight: 30
title: "Tutorial: Using Existing Services"
description: Consume the standard Axial service packages from an explicit environment.
---

# Tutorial: Using Existing Services

Axial ships with a few reusable service packages such as clock, logging, environment variables, console, filesystem, HTTP, and process execution.

These are still explicit dependencies. The workflow only sees them when your environment provides them.

## Build An Environment

```fsharp
open Axial
open Axial.Flow.PlatformService

type AppEnv =
    { Runtime: BaseRuntime }

    interface IHas<IClock> with
        member this.Service = this.Runtime.Clock

    interface IHas<ILog> with
        member this.Service = this.Runtime.Log

    interface IHas<IEnvironmentVariables> with
        member this.Service = this.Runtime.EnvironmentVariables
```

## Use The Services

```fsharp
let loadMode : Flow<AppEnv, EnvironmentVariableError, string> =
    flow {
        let! now = Clock.utcDateTime
        let! mode = EnvironmentVariable.get "APP_MODE"
        do! Log.info $"[{now:O}] starting in mode {mode}"
        return mode
    }
```

The workflow does not know where the clock, logger, or environment variables came from. It only knows the service contracts.

## Run It

```fsharp
let run () = task {
    let env = { Runtime = BaseRuntime.liveValue }
    let! exit = loadMode.ToTask(env)
    printfn "%A" exit
}
```

If you already have several standard services, wrapping them once in an app environment is usually the cleanest boundary.

Continue with [Tutorial: Creating Reusable Services](./custom-services/) when you need your own service contract alongside the built-in ones.
