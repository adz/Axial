---
title: "Tutorial: Using Existing Services"
description: Consume the standard Axial service packages from an explicit environment.
---

# Tutorial: Using Existing Services

Axial ships with a few reusable service packages such as clock, logging, environment variables, console, filesystem, HTTP, and process execution.

These are still explicit dependencies. The workflow only sees them when your environment provides them.

## Build An Environment

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

## Use The Services

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
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

```fsharp no-check reason="Shown independently; surrounding application context is intentionally omitted"
let run () = task {
    let env = { Runtime = BaseRuntime.liveValue }
    let! exit = loadMode.StartAsTask(env)
    printfn "%A" exit
}
```

If you already have several standard services, wrapping them once in an app environment is usually the cleanest boundary.

Continue with [Tutorial: Creating Reusable Services](custom-services.html) when you need your own service contract alongside the built-in ones.
