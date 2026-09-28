---
title: Run a process in an application Flow
description: Supply processes as an explicit, fakeable application capability.
---

# Run a process in an application Flow

An Axial Flow receives the application's environment record when it runs. If part of the application needs to start
commands, put a process service in that record. The Flow reads that service when it reaches a process step.

In production, the record contains Axial's live process service. In a unit test, it can contain a small fake that
returns the result the test needs. The application code that builds the command does not change.

## Put a process service in the application record

[`Process.live`](xref:M:Axial.Process.Process.live) builds the real service. It is a function because it combines the
services it needs. [`Clock.live`](xref:M:Axial.PlatformService.Clock.live), [`FileSystem.live`](xref:M:Axial.FileSystem.FileSystem.live), and [`Console.live`](xref:M:Axial.Console.Console.live) are already-created service values, so they are its arguments.

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
open Axial.Console
open Axial.FileSystem
open Axial.PlatformService
open Axial.Process

type AppEnvironment =
    { Processes: IProcess }
    interface IHasProcess with member this.Process = this.Processes

let appEnvironment =
    { Processes = Process.live Clock.live FileSystem.live Console.live }
```

The clock timestamps execution. The filesystem opens file input and output targets. The console receives configured
console output. The live process builder combines them into the `Processes` value; it does not start a process.

## Convert a command to a Flow

[`Process.toFlow`](xref:M:Axial.Process.Process.toFlow) makes the command one lazy step in the application workflow. [`Flow.run`](xref:M:Axial.Flow.run) starts the complete
application Flow with its environment.

```fsharp
let readRevision : Flow<AppEnvironment, ProcessError, string> =
    flow {
        let! result =
            Process.command $"git rev-parse --short HEAD"
            |> Process.capture

        return result.StdOut.Trim()
    }

```

```fsharp run
match Flow.run appEnvironment readRevision with
| Exit.Success revision -> (revision.Length >= 7) |> shouldEqual true
| other -> failwithf "unexpected %A" other
```

[`Process.capture`](xref:M:Axial.Process.Process.capture) and [`Process.console`](xref:M:Axial.Process.Process.console) are convenient forms that configure output and then create a Flow.
The latter preserves the output policy already carried by a `ProcessSpec`.

## Put a fake in the record for a unit test

`IProcess` is the interface used by the `Processes` field. It has two operations: `Run` returns the completed result,
and `Stream` returns output events. A focused test usually needs only `Run`; return a prepared `ProcessResult` and
inspect the received specification when the test cares about command construction.

```fsharp
let expectedResult : ProcessResult =
    { ExitCode = 0
      ExitCodes = [ 0 ]
      StdOut = "abc1234\n"
      StdErr = ""
      StdOutCapture = { Text = "abc1234\n"; Bytes = [||]; Truncated = false }
      StdErrCapture = { Text = ""; Bytes = [||]; Truncated = false }
      Stages = []
      StartedAt = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
      Duration = TimeSpan.Zero }

let mutable requested = None

let fakeProcess =
    { new IProcess with
        member _.Run specification =
            requested <- Some specification
            Flow.succeed expectedResult

        member _.Stream _ =
            FlowStream.singleton (ProcessEvent.Completed expectedResult) }

let testEnvironment = { Processes = fakeProcess }
```

```fsharp run
Flow.run testEnvironment readRevision |> shouldEqual (Exit.Success "abc1234")
requested |> Option.map Process.render |> shouldEqual (Some "git rev-parse --short HEAD")
```

This keeps a unit test deterministic: it starts no native child process, and it can assert the executable, arguments,
working directory, timeout, and output policy on `requested`. Use the live interpreter in integration tests when the
contract with the operating system matters.

## Use an existing application environment at a host boundary

[`Process.runWith`](xref:M:Axial.Process.Process.runWith) is for a command-line host that already owns a larger environment. It runs a
`ProcessError` workflow with that record and converts failure to a process exit code. The environment must also supply
`IConsole`, used for error reporting. Most applications instead call [`Flow.run`](xref:M:Axial.Flow.run) themselves and handle the resulting
`Exit` according to their own boundary policy.
