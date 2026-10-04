---
title: Processes
description: Start external commands from an Axial workflow.
---

# Processes

`Axial.Process` lets a command-line program start an external command without giving up typed failures, cancellation,
or cleanup. You describe the command first, choose what happens to its input and output, then run it.

## A script

For a small command-line program, describe commands, connect them, choose where output goes, then call `run`:

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
// The examples on this page start real processes with the live service.
let host : ProcessHostEnvironment =
    { Process = Process.live Clock.live FileSystem.live Console.live
      Console = Console.live
      Clock = Clock.live }

let stdoutOf (workflow: Flow<ProcessHostEnvironment, ProcessError, ProcessResult>) : string =
    match Flow.run host workflow with
    | Exit.Success result -> result.StdOut
    | Exit.Failure cause -> failwithf "%A" cause
```

```fsharp
open Axial.Process.DSL // Short pipeline helpers: cwd, env, timeout, =>, toFlow, run

let recentCommits (repository: string) =
    cmd $"git log --oneline -20"
    |> cwd repository
    |> env "NO_COLOR" "1"
    |> timeout (TimeSpan.FromSeconds 5.0)
    => cmd $"head -5"
    |> toFlow

// The outcome runner returns Exit<ProcessResult, ProcessError>.
let script () = recentCommits "." |> run
```

```fsharp run
let lines = stdoutOf (recentCommits ".") |> _.Split('\n', StringSplitOptions.RemoveEmptyEntries)
(lines.Length >= 1 && lines.Length <= 5) |> shouldEqual true
```

`Process.command` (DSL `cmd`) returns an immutable `ProcessCommand` and preserves each interpolation hole
as one native argument. Configure command settings before connecting stages with `=>` or `Process.pipe`, which
returns a `ProcessSpec`. Construction starts no child process. `toFlow` makes the topology one lazy workflow step.

`run` supplies live services and returns the complete `Exit`; direct command execution captures full stdout.
`runExitCode` returns an integer for an entry point and defaults to visible console output without stdout capture.
Both accept commands, specs, or an already constructed Flow. Explicit capture and routing settings win.

For a server, worker, or application Flow, see [run a process in an application Flow](running-in-an-application-flow.html).

## Choose how the command communicates

| Need | API | Result |
| --- | --- | --- |
| Inspect output after completion | `toFlow` or direct `run` | `ProcessResult` inside a Flow or Exit |
| Show output as it arrives | `stdoutToConsole` / `stderrToConsole` | Configured command; capture stays independent |
| React to lines before completion | `streamStdoutLines` | `ProcessStream` with completion-aware terminals |
| Send output to a file | `stdoutToFile` / `stdoutToAppendFile` | Configured command |
| Return a command-line status | `runExitCode` | Integer exit code |

Batch execution captures full stdout and a separate 64 KiB stderr tail. Streaming retains bounded 64 KiB tails.
`captureAll`, `captureTail`, and `captureNothing` affect stdout only; stderr has explicit matching helpers.
See [output capture and destinations](output-capture.html) and [streaming output](streaming.html).

## What Axial owns

The process service starts the native topology; Axial owns its lifetime. A timeout or interruption terminates every started stage, including partially started pipelines, before the enclosing Flow finishes. Expected process problems remain `ProcessError` values instead of becoming unstructured exceptions.

## Guides

- [Commands, input, and pipelines](composition.html)
- [Output capture and destinations](output-capture.html)
- [Streaming output](streaming.html)
- [Failures and transcripts](failures-and-transcripts.html)
- [Scripts and shell syntax](scripts.html)
- [DSL recipes: input, routing, fan-in, and parallel commands](dsl-recipes.html)
- [Worked examples](worked-examples.html)
- [Run a process in an application Flow](running-in-an-application-flow.html)
- [Fable](fable.html)
