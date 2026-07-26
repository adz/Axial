---
title: Fable
description: Use process specifications with a supplied interpreter on non-native targets.
weight: 50
type: docs
---

`ProcessSpec`, `ProcessResult`, `ProcessError`, the DSL, and `IProcess` are target-neutral. A browser cannot start an operating-system process, so `Process.live` and native stream adapters are available only on .NET.

Fable applications can construct specifications and run them against an `IProcess` implementation that delegates execution to a worker or another host:

```fsharp
type AppEnvironment =
    { Process: IProcess }
    interface IHas<IProcess> with member this.Service = this.Process

let workflow =
    Process.command "device-tool" [ "inspect" ]
    |> Process.run<AppEnvironment>
```

An interpreter returns lazy `Flow<unit, ProcessError, ProcessResult>` and `FlowStream<unit, ProcessError, ProcessEvent>` values. It must not start work while constructing those values.
