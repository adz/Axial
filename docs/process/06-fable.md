---
title: Fable
description: Use process specifications with a supplied interpreter on non-native targets.
platform: fable
---

`ProcessCommand`, `ProcessSpec`, `ProcessStream`, `ProcessResult`, `ProcessError`, the DSL, and `IProcess` are target-neutral. A browser cannot start an operating-system process, so [`Process.live`](xref:M:Axial.Process.Process.live) and native stream adapters are available only on .NET.

Fable applications can construct specifications and run them against an `IProcess` implementation that delegates execution to a worker or another host:

```fsharp no-check reason="Requires Fable compiler validation; FsLiveDocs currently checks .NET contexts only"
type AppEnvironment =
    { Process: IProcess }
    interface IHasProcess with member this.Process = this.Process

let workflow =
    Process.command $"device-tool inspect"
    |> Process.toFlow
```

An interpreter implements `IProcess.Execute`, returning a lazy `Flow<unit, ProcessError, ProcessResult>` and
awaiting the supplied observer serially with backpressure. It must not start work while constructing the Flow.
Streaming constructors additionally require an `IHasClock` environment for Axial's supervised producer.
