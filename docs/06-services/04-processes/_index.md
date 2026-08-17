---
title: Processes
description: Run external commands as typed Axial workflows.
---

`Axial.Process` represents external work as an immutable `ProcessSpec`. Building a specification performs no I/O. `Process.run` asks the `IProcess` service to interpret it in the current Flow runtime.

```fsharp no-check reason="Shown independently; surrounding application context is intentionally omitted"
open Axial.Process

let version =
    Process.command "dotnet" [ "--version" ]
    |> Process.timeout (TimeSpan.FromSeconds 10)
    |> Process.run
```

`Process.command` creates a runnable one-stage specification. Configuration functions return updated values, and `Process.pipe` connects specifications through real standard streams. `Process.run` returns `Flow<#IHasProcess, ProcessError, ProcessResult>`; `Process.stream` returns output and completion events with backpressure.

The live interpreter receives its operational dependencies explicitly:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let process = Process.live clock fileSystem console
```

Supply it by implementing `IHasProcess` on the environment given to the workflow:

```fsharp
type AppEnv =
    { Processes: IProcess }
    interface IHasProcess with member this.Process = this.Processes
```

Flow owns scheduling, cancellation, timeout racing, and scope cleanup. The live interpreter translates interruption into process-tree termination and cleans up every stage that started, including partial startup.

## Guides

- [Commands and composition](composition.html)
- [Output and streaming](output-streaming.html)
- [Failures and transcripts](failures-transcripts.html)
- [Scripts](scripts.html)
- [Fable](fable.html)
