---
title: Processes
description: Start external commands from an Axial workflow.
---

# Processes

`Axial.Process` describes external commands and connects them to Axial workflows. Use `toFlow` to make a command
one step in a larger `Flow`, alongside other application work. For short scripts, the `run` and `runExitCode`
helpers execute commands directly. Both approaches retain typed failures, cancellation, and process cleanup.

## Run one command

Open `Axial.Process.DSL` to construct a command with `cmd`, then execute it directly with `run`.
You can start with a single command without writing a `flow { }` workflow or configuring an application.
`cmd` returns a `ProcessCommand`; `run` returns an `Exit` containing the captured output or failure.
`Exit.Success` holds a `ProcessResult` with `StdOut`, `StdErr`, and exit codes.
`Exit.Failure` holds a `Cause<ProcessError>`: an expected process error, a defect, or interruption.

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
open Axial
open Axial.Process
open Axial.Process.DSL

let greeting : ProcessCommand = cmd $"printf hello"
let outcome : Exit<ProcessResult, ProcessError> = greeting |> run
```

```fsharp run
match outcome with
| Exit.Success result -> shouldEqual "hello" result.StdOut
| other -> failwithf "Expected greeting, got %A" other
```

## Adjust that command

Add the working directory, an environment variable, and a per-command timeout before running it.
Each setting returns another `ProcessCommand`; applying settings starts no process:

```fsharp
let configuredGreeting (directory: string) : ProcessCommand =
    cmd $"printf hello"
    |> cwd directory
    |> env "GREETING_MODE" "plain"
    |> timeout (TimeSpan.FromSeconds 5.)
```

```fsharp run
match configuredGreeting (Path.GetTempPath()) |> run with
| Exit.Success result -> shouldEqual "hello" result.StdOut
| other -> failwithf "Expected configured greeting, got %A" other
```

## Connect a second command

`=>` connects the first command's stdout to the second command's stdin. Keep each command's settings before
its connection. Joining two commands produces a `ProcessSpec`, which describes the connected topology:

```fsharp
let loudGreeting (directory: string) : ProcessSpec =
    cmd $"printf hello"
    |> cwd directory
    |> env "GREETING_MODE" "plain"
    |> timeout (TimeSpan.FromSeconds 5.)
    => cmd $"tr '[:lower:]' '[:upper:]'"
```

```fsharp run
match loudGreeting (Path.GetTempPath()) |> run with
| Exit.Success result -> shouldEqual "HELLO" result.StdOut
| other -> failwithf "Expected piped greeting, got %A" other
```

## The same pipeline with qualified functions

The DSL is a shorter way to construct the same commands and connections. This version uses the `Process`
functions throughout and needs no DSL names or operators:

```fsharp
let loudGreetingWithFunctions (directory: string) : ProcessSpec =
    Process.command $"printf hello"
    |> Process.workingDirectory directory
    |> Process.environment "GREETING_MODE" "plain"
    |> Process.timeout (TimeSpan.FromSeconds 5.)
    |> Process.pipe (Process.command $"tr '[:lower:]' '[:upper:]'")
```

```fsharp run
match loudGreetingWithFunctions (Path.GetTempPath()) |> Process.run with
| Exit.Success result -> shouldEqual "HELLO" result.StdOut
| other -> failwithf "Expected equivalent pipeline, got %A" other
```

`Process.command` (DSL `cmd`) returns an immutable `ProcessCommand` and preserves each interpolation hole
as one native argument. Configure command settings before connecting stages with `=>` or `Process.pipe`, which
returns a `ProcessSpec`. Construction starts no child process. `toFlow` makes the topology one lazy workflow step.

`run` executes directly and returns the complete `Exit`; direct command execution captures full stdout.
`runExitCode` returns an integer for an entry point and defaults to visible console output without stdout capture.
Both accept commands, specs, or an already constructed Flow. Explicit capture and routing settings win.

## The types at each step

| Type | What it represents | Typical construction |
| --- | --- | --- |
| `ProcessCommand` | One immutable command, arguments, and per-command settings | `cmd`, `cwd`, `env`, routing helpers |
| `ProcessSpec` | Connected commands and topology policies | `=>`, `pipeTo`, `mergeStdout`, `stageDefaults` |
| `ProcessResult` | Exit codes, retained output, capture metadata, and stage results | Successful execution |
| `Exit<ProcessResult, ProcessError>` | The finished outcome, including a failure cause | `commandOrSpec |> run` |
| `Flow<'env, ProcessError, ProcessResult>` | A lazy process step inside an application workflow | `commandOrSpec |> toFlow` |
| `ProcessStream<'env, string>` | Lazy decoded lines with completion tracking | `streamStdoutLines`, `streamStderrLines` |
| `ProcessStream<'env, ProcessOutput>` | Lazy output observations with stage and channel metadata | `streamStdout`, `streamStderr`, `streamOutputs` |
| `ProcessCompletion` | `Completed result` after natural completion, or `StoppedEarly` after owned prefix cleanup | A `ProcessStream` terminal |

`'env` describes the dependencies required by an application Flow. Short scripts can use `run` without
constructing that record themselves. `toFlow` and stream constructors describe work without starting it.
A stream terminal produces a Flow; `run` executes that Flow and returns its outcome. Collection and fold
terminals include the consumed value together with `ProcessCompletion`.

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
