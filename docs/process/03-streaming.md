---
title: Streaming output
description: Consume process output while a command is still running.
---

# Streaming output

Use [`Process.stream`](xref:M:Axial.Process.Process.stream) when a process's output is part of the workflow, rather than something to inspect after the process exits. It returns a `FlowStream`, not a `Flow`.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Console
open Axial.FileSystem
open Axial.PlatformService
open Axial.Process

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
// The examples on this page start real processes with the live service.
let host : ProcessHostEnvironment =
    { Process = Process.live Clock.live FileSystem.live Console.live
      Console = Console.live }
```

```fsharp
let events =
    Process.command $"printf 'ready\nsample 1\n'"
    |> Process.framing OutputFraming.Lines
    |> Process.stream
```

With `OutputFraming.Lines`, each `ProcessEvent.Output` contains one decoded line. `OutputFraming.Chunks` preserves arbitrary decoded chunks. Every output value identifies its stage, stdout or stderr channel, and timestamp. The final event is always `ProcessEvent.Completed result`.

## Handle events as they arrive

Use ordinary FlowStream combinators to take, filter, transform, or collect events:

```fsharp
let report event =
    match event with
    | ProcessEvent.Output output -> $"[{output.Channel}] {output.Text}"
    | ProcessEvent.Completed result -> $"exit code: {result.ExitCode}"

let reports : Flow<ProcessHostEnvironment, ProcessError, string list> =
    events
    |> FlowStream.map report
    |> FlowStream.runCollect
```

```fsharp run
reports |> Flow.run host |> shouldEqual (Exit.Success [ "[StdOut] ready"; "[StdOut] sample 1"; "exit code: 0" ])
```

The stream is backpressured: Axial does not buffer unbounded events while the consumer is busy. Stopping consumption early interrupts the producer Flow and terminates the native process topology before the enclosing scope closes.

## Stream versus console forwarding

Use DSL [`console`](xref:M:Axial.Process.DSL.console), or configure both channels with [`Process.stdout`](xref:M:Axial.Process.Process.stdout) and [`Process.stderr`](xref:M:Axial.Process.Process.stderr), when output only needs to be visible. That path returns a normal `Flow` that completes with `ProcessResult`. Use [`Process.stream`](xref:M:Axial.Process.Process.stream) when output itself drives workflow decisions.
