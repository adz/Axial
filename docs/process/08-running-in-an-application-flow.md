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
open Axial.Process.DSL

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
open Axial.Console
open Axial.FileSystem
open Axial.PlatformService
open Axial.Process
open Axial.Process.DSL

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
            cmd $"git rev-parse --short HEAD"
            |> toFlow

        return result.StdOut.Trim()
    }

```

```fsharp run
match Flow.run appEnvironment readRevision with
| Exit.Success revision -> (revision.Length >= 7) |> shouldEqual true
| other -> failwithf "unexpected %A" other
```

`Process.toFlow` preserves routing and capture policies. Batch execution captures all stdout and a separate
64 KiB stderr tail by default. Streaming constructors require `IHasClock` in the application environment as well.

## Put a fake in the record for a unit test

`IProcess.Execute` is the single service operation. It returns a lazy completion Flow and awaits each supplied
observer with backpressure. A focused batch test can return a prepared result without emitting output.

```fsharp
let expectedResult : ProcessResult =
    { ExitCode = 0
      ExitCodes = [ 0 ]
      StdOut = "abc1234\n"
      StdErr = ""
      StdOutCapture =
          { Text = "abc1234\n"
            Bytes = System.Text.Encoding.UTF8.GetBytes "abc1234\n"
            Truncated = false
            Available = true }
      StdErrCapture = { Text = ""; Bytes = [||]; Truncated = false; Available = true }
      Stages = []
      StartedAt = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
      Duration = TimeSpan.Zero }

let mutable requested = None

let fakeProcess =
    { new IProcess with
        member _.Execute(specification, _) =
            Flow.delay (fun () ->
                requested <- Some specification
                Flow.succeed expectedResult) }

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

`Process.tryRunWith` runs commands, specs, or an application Flow with the supplied environment and returns the
complete `Exit`. Command execution needs only `IHasProcess`; it does not require a reporting console.
`Process.runWith` returns the successful value but raises `ProcessRunException` on failure. In an application,
prefer `Flow.run environment` for the enclosing workflow; both Process runners are boundary conveniences.
