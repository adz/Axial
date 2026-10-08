---
title: Processes
description: Run external commands as standalone tools or as steps in an Axial workflow.
---

# Processes

A command starts as a description, not a running process. Configure it, connect commands into a pipeline if needed,
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

`Process.run` is the short path for scripts: it returns a successful value and raises `ProcessRunException` on
failure. Use `Process.tryRun` when the caller needs to inspect the full `Exit`. In an application, convert a command
to a lazy `Flow` with `toFlow` and bind it into the surrounding `flow { }` instead.

The distinction matters: command construction does no I/O; execution does. Axial owns the child processes it starts,
including their cancellation and cleanup. It does not invoke a shell unless you explicitly choose one.

## Guides

Follow these in order to learn the model. Start with one command, then compose it, then place it inside an application.

1. [Commands and composition](composition.html) — arguments, settings, pipelines, and plans.
2. [Run a process in an application Flow](running-in-an-application-flow.html) — provide the process service and compose with application work.
3. [Worked examples](worked-examples.html) — see the pieces together.

## How-to

Jump to the task you need. Each page focuses on one decision rather than explaining the whole API again.

- [Capture output or send it elsewhere](output-capture.html)
- [Consume output as it arrives](streaming.html)
- [Handle failures and inspect results](failures-and-transcripts.html)
- [Write a script or use shell syntax](scripts.html)
- [Use stdin, routes, fan-in, or parallel commands](dsl-recipes.html)
- [Use Axial.Process from Fable](fable.html)

## Reference

The [Axial.Process API reference](xref:T:Axial.Process.ProcessCommand) lists the public types and members. Authored
API introductions to [`Process`](xref:M:Axial.Process.Process.command) and the
[`DSL`](xref:M:Axial.Process.DSL.cmd) explain how the main groups fit together. Use the reference when you know the
name and need its signature or exact contract. The guides teach the choices.
