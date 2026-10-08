---
title: Worked examples
description: Three complete shapes for using Axial.Process.
---

# Worked examples

These examples go from a command-line script to an application Flow. Each command is a value until a
Flow runner reaches the host boundary.

## A command-line tool that shows progress

A command-line entry point can forward output through the console and choose its status from `Exit`.
`Process.tryRun` preserves the cause so the entry point can report startup failures and interruptions.

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
open Axial.Process.DSL

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
open Axial.Process.DSL

// In a program, mark this [<EntryPoint>].
let main _ =
    let outcome =
        cmd $"dotnet --info"
        |> stdoutToConsole
        |> Process.tryRun
    match outcome with
    | Exit.Success result -> result.ExitCode
    | Exit.Failure(Cause.Fail error) ->
        eprintfn "%s" (ProcessError.describe error)
        ProcessError.exitCode error
    | Exit.Failure cause ->
        eprintfn "%A" cause
        1
```

There is no separate process runtime to construct. Console forwarding is configured lazily; `Process.tryRun` starts the command.

## Use a command as one step in an application

Use [`toFlow`](xref:M:Axial.Process.DSL.toFlow) when the command is one step among other application work. The application supplies its own `IProcess`
service and decides how to use the result.

```fsharp
let gitRevision : Flow<#IHasProcess, ProcessError, string> =
    flow {
        let! result =
            cmd $"git rev-parse --short HEAD"
            |> toFlow

        return result.StdOut.Trim()
    }

let buildLabel =
    flow {
        let! revision = gitRevision
        return $"build-{revision}"
    }

let appEnvironment = ProcessHostEnvironment.live
```

```fsharp run
match Flow.run appEnvironment buildLabel with
| Exit.Success label -> label.StartsWith "build-" |> shouldEqual true
| other -> failwithf "unexpected %A" other
```

`toFlow` uses batch capture defaults. `Flow.run` executes the enclosing application workflow with its explicit
environment.

## Stream lines into workflow decisions

`streamStdoutLines` yields only stdout lines. Its terminal returns values and completion separately.

```fsharp
let collectEvents =
    cmd $"printf '{{\"event\":\"ready\"}}\n{{\"event\":\"done\"}}\n'"
    |> streamStdoutLines
    |> ProcessStream.runCollect
    |> Flow.map fst

```

```fsharp run
Flow.run appEnvironment collectEvents
|> shouldEqual (Exit.Success [ """{"event":"ready"}"""; """{"event":"done"}""" ])
```

The consumer pulls the stream. If the enclosing Flow is interrupted or the consumer stops early, Axial interrupts the
native process topology and waits for its cleanup.

For more checked command-line patterns, see [DSL recipes](dsl-recipes.html).

For command construction and pipelines, continue with [commands, input, and pipelines](composition.html). For output
destinations and bounded capture, see [output capture and destinations](output-capture.html).
