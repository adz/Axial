---
title: Processes
description: Run external commands as standalone tools or as steps in an Axial workflow.
---

# Processes

A command is a description, not a running process. Configure it, connect commands into a pipeline if needed,
then choose where to run it:

```fsharp prepare
open Axial.Process
open Axial.Process.DSL
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
let command = cmd $"printf hello"
```

```fsharp run
command |> Process.run |> _.StdOut |> shouldEqual "hello"
```

For scripts, `Process.run` returns a successful value and raises `ProcessRunException` on
failure. Use `Process.tryRun` when the caller needs to inspect the full `Exit`. In an application, convert a command
to a lazy `Flow` with `toFlow` and bind it into the surrounding `flow { }` instead.

Command construction does no I/O; execution does. Axial owns the child processes it starts,
including their cancellation and cleanup. It does not invoke a shell unless you explicitly choose one.

## Guides

1. [Commands and composition](composition.html) — arguments, settings, pipelines, and plans.
2. [Run a process in an application Flow](running-in-an-application-flow.html) — provide the process service and compose with application work.
3. [Worked examples](worked-examples.html) — complete process workflows.

## How-to

- [Capture output or send it elsewhere](output-capture.html)
- [Consume output as it arrives](streaming.html)
- [Integrate process streams with FlowStream](flowstream-integration.html)
- [Handle failures and inspect results](failures-and-transcripts.html)
- [Write a script or use shell syntax](scripts.html)
- [Use stdin, routes, fan-in, or parallel commands](dsl-recipes.html)
- [Use Axial.Process from Fable](fable.html)

## Reference

The [Axial.Process API reference](xref:T:Axial.Process.ProcessCommand) lists the public types and members. Authored
API introductions to [`Process`](xref:M:Axial.Process.Process.command) and the
[`DSL`](xref:M:Axial.Process.DSL.cmd) explain how the main groups fit together. Use the reference when you know the
name and need its signature or exact contract. The guides cover when to use each operation.
