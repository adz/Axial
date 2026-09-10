---
title: Worked examples
description: Three complete shapes for using Axial.Process.
---

# Worked examples

These examples start with a command-line script and build up to an application Flow. Each command is a value until a
Flow runner reaches the host boundary.

## A command-line tool that shows progress

Use [`console`](xref:M:Axial.Process.DSL.console) when the command's output is for the person running the program. [`run`](xref:M:Axial.Process.DSL.run) supplies live services and turns
the Flow outcome into the program's exit code.

```fsharp no-check reason="Runs the locally installed dotnet command through live services"
open Axial.Process.DSL

[<EntryPoint>]
let main _ =
    cmd $"dotnet --info"
    |> console
    |> run
```

There is no separate process runtime to construct. Console forwarding is lazy; the final runner starts the command.

## Use a command as one step in an application

Use [`toFlow`](xref:M:Axial.Process.DSL.toFlow) when the command is one step among other application work. The application supplies its own `IProcess`
service and decides how to use the result.

```fsharp no-check reason="The application environment and its live services are shown in the surrounding guide"
open Axial.Process

let gitRevision : Flow<#IHasProcess, ProcessError, string> =
    flow {
        let! result =
            Process.command $"git rev-parse --short HEAD"
            |> Process.capture

        return result.StdOut.Trim()
    }

let buildLabel =
    flow {
        let! revision = gitRevision
        return $"build-{revision}"
    }

let outcome = Flow.run appEnvironment buildLabel
```

[`capture`](xref:M:Axial.Process.DSL.capture) gives this command explicit complete-output policy. [`Flow.run`](xref:M:Axial.Flow.run) runs the enclosing application Flow; it does
not create or replace the application's environment.

## Stream lines into workflow decisions

Use [`stream`](xref:M:Axial.Process.DSL.stream) when each line should affect the workflow before the command exits. Here a producer reports JSON-like
events; the workflow selects only stdout lines and collects them under Flow's cancellation and cleanup scope.

```fsharp no-check reason="The application environment and event parser are intentionally application-specific"
open Axial.Process

let collectEvents =
    Process.command $"device-tool watch --format jsonl"
    |> Process.framing OutputFraming.Lines
    |> Process.stream
    |> FlowStream.choose (function
        | ProcessEvent.Output output when output.Channel = OutputChannel.StdOut ->
            Some output.Text
        | _ -> None)
    |> FlowStream.runCollect

let outcome = Flow.run appEnvironment collectEvents
```

The consumer pulls the stream. If the enclosing Flow is interrupted or the consumer stops early, Axial interrupts the
native process topology and waits for its cleanup.

For command construction and pipelines, continue with [commands, input, and pipelines](composition.html). For output
destinations and bounded capture, see [output capture and destinations](output-capture.html).
