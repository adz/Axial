---
title: Streaming output
description: Consume process output while a command is still running.
---

# Streaming output

Use [`Process.stream`](xref:M:Axial.Process.Process.stream) when a process's output is part of the workflow, rather than something to inspect after the process exits. It returns a `FlowStream`, not a `Flow`.

```fsharp no-check reason="Application-specific process environment is omitted"
let events =
    Process.command $"device-tool watch"
    |> Process.framing OutputFraming.Lines
    |> Process.stream
```

With `OutputFraming.Lines`, each `ProcessEvent.Output` contains one decoded line. `OutputFraming.Chunks` preserves arbitrary decoded chunks. Every output value identifies its stage, stdout or stderr channel, and timestamp. The final event is always `ProcessEvent.Completed result`.

## Handle events as they arrive

Use ordinary FlowStream combinators to take, filter, transform, or collect events:

```fsharp no-check reason="Illustrates FlowStream event handling; application host is omitted"
let report event =
    match event with
    | ProcessEvent.Output output -> printfn "[%A] %s" output.Channel output.Text
    | ProcessEvent.Completed result -> printfn "exit code: %d" result.ExitCode

let! collected =
    Process.command $"device-tool watch"
    |> Process.framing OutputFraming.Lines
    |> Process.stream
    |> FlowStream.runCollect

collected |> List.iter report
```

The stream is backpressured: Axial does not buffer unbounded events while the consumer is busy. Stopping consumption early interrupts the producer Flow and terminates the native process topology before the enclosing scope closes.

## Stream versus console forwarding

Use DSL [`console`](xref:M:Axial.Process.DSL.console), or configure both channels with [`Process.stdout`](xref:M:Axial.Process.Process.stdout) and [`Process.stderr`](xref:M:Axial.Process.Process.stderr), when output only needs to be visible. That path returns a normal `Flow` that completes with `ProcessResult`. Use [`Process.stream`](xref:M:Axial.Process.Process.stream) when output itself drives workflow decisions.
