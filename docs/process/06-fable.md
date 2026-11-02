---
title: Fable
description: Use process specifications with a supplied interpreter on non-native targets.
platform: fable
---

`ProcessCommand`, `ProcessSpec`, `ProcessStream`, `ProcessResult`, `ProcessError`, the DSL, and `IProcess` are target-neutral. A browser cannot start an operating-system process, so [`Process.live`](xref:M:Axial.Process.Process.live) and native stream adapters are available only on .NET.

Fable applications can construct specifications and run them against an `IProcess` implementation that delegates execution to a worker or another host:

```fsharp no-check reason="Requires Fable compiler validation; FsLiveDocs currently checks .NET contexts only"
open Axial.Process.DSL

type AppEnvironment =
    { Process: IProcess }
    interface IHasProcess with member this.Process = this.Process

let workflow =
    cmd $"device-tool inspect"
    |> toFlow
```

An interpreter implements `IProcess.Execute`, returning a lazy `Flow<unit, ProcessError, ProcessResult>` and
awaiting the supplied observer serially with backpressure. It must not start work while constructing the Flow.
Streaming constructors need no clock member in the environment: the supervised producer reads the runtime clock.

`workflow` above is construction-only: it builds a specification and a Flow that asks for `IProcess` when run, and
nothing executes until then. Supply an interpreter that delegates execution to a worker or another host, for example:

```fsharp no-check reason="Requires Fable compiler validation; FsLiveDocs currently checks .NET contexts only"
open Axial

/// A minimal interpreter that delegates to a worker and returns its result locally.
type WorkerProcess(execute: ProcessSpec -> Async<ProcessResult>) =
    interface IProcess with
        member _.Execute(specification, observe) =
            Flow.fromAsync (execute specification)
```

`Flow.fromAsync` treats a worker exception as a defect; use `Flow.attemptAsync` and map the exception into
`ProcessError` when the worker reports typed failures.
