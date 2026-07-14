---
title: Processes
description: Run external commands as typed Axial workflows.
weight: 40
---

`Axial.Flow.Process` represents external work as an immutable `ProcessSpec`. Building a specification performs no I/O. `Process.run` asks the `IProcess` service to interpret it in the current Flow runtime.

```fsharp
open Axial.Flow.Process

let version =
    Process.command "dotnet" [ "--version" ]
    |> Process.timeout (TimeSpan.FromSeconds 10)
    |> Process.run
```

`Process.command` creates a runnable one-stage specification. Configuration functions return updated values, and `Process.pipe` connects specifications through real standard streams. `Process.run` returns `Flow<#IHas<IProcess>, ProcessError, ProcessResult>`; `Process.stream` returns output and completion events with backpressure.

The live interpreter receives its operational dependencies explicitly:

```fsharp
let process = Process.live clock fileSystem console
```

Flow owns scheduling, cancellation, timeout racing, and scope cleanup. The live interpreter translates interruption into process-tree termination and cleans up every stage that started, including partial startup.

## Guides

- [Commands and composition](composition/)
- [Output and streaming](output-streaming/)
- [Failures and transcripts](failures-transcripts/)
- [Scripts](scripts/)
- [Fable](fable/)
