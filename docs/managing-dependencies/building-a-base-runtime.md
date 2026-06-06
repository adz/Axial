---
weight: 40
title: Building a Base Runtime
description: Supplying clock, logging, random, GUID, and environment-variable services explicitly.
---

# Building a Base Runtime

Clock, logging, random, GUID, and environment-variable access are explicit services. `FsFlow.Services.Core` provides a
`BaseRuntime` record that groups the standard services most hosts need:

```fsharp
type BaseRuntime =
    { Clock: IClock
      Log: ILog
      Random: IRandom
      Guid: IGuid
      EnvironmentVariables: IEnvironmentVariables }
```

`BaseRuntime` implements `IHas<'service>` for each service, so helpers such as `Clock.now` and `EnvironmentVariable.get`
work against it directly.

## Live Runtime

Use `BaseRuntime.liveValue` when you already want a concrete value:

```fsharp
let result =
    (Clock.now).RunSynchronously(BaseRuntime.liveValue)
```

Use `BaseRuntime.live` when composing with layers:

```fsharp
let runnable =
    workflow
    |> Flow.provide BaseRuntime.live
```

## Provider-Backed Runtime

Use `BaseRuntime.fromServiceProvider` when a .NET host container owns the service implementations and you want typed
startup validation.

```fsharp
let runnable =
    workflow
    |> Flow.provide BaseRuntime.fromServiceProvider
```

The provider must contain `IClock`, `ILog`, `IRandom`, `IGuid`, and `IEnvironmentVariables`. Missing registrations are
reported as `BaseRuntimeError.MissingService`.

## Custom Runtime

Tests can construct a deterministic base runtime directly:

```fsharp
let testRuntime =
    { Clock = Clock.fromValue fixedNow
      Log = Log.live
      Random = Random.fromValue 4
      Guid = Guid.fromValue fixedGuid
      EnvironmentVariables = EnvironmentVariables.fromPairs [ "MODE", "test" ] }
```

No override API is required. The override is just the environment value passed to the flow.
