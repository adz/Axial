---
title: Failures and transcripts
description: Handle structured process failures and inspect complete execution results.
---

When a process cannot complete as requested, [`Process.toFlow`](xref:M:Axial.Process.Process.toFlow) fails with
`ProcessError`. This is the Flow's normal typed error value, not an exception thrown by the child program. Run the
Flow and match `Exit.Failure(Cause.Fail error)` when this boundary needs to decide what to report or return.

## The failure cases

| `ProcessError` case | When it occurs | Details available |
| --- | --- | --- |
| `StartFailed` | The executable cannot be started: it is missing, not executable, or the operating system rejects startup. A pipeline can also fail this way after earlier stages have started. | `ProcessStartFailure.Command` and `Message` |
| `TimedOut` | The specification's configured timeout elapses before every stage completes. | Redacted specification and timeout duration |
| `StageFailed` | A started command exits with a code outside its configured success-code set. This is the usual result for a command that reports failure with a nonzero exit code. | Failed `StageResult` and the complete `ProcessResult` |
| `IoFailed` | Axial cannot read, write, or route one of the process streams or configured file targets. | I/O error message |

For every timeout, cancellation, and partial pipeline startup, Axial terminates stages that did start before the Flow
finishes. The typed error tells the caller what happened after cleanup, rather than leaving a child process running in
the background.

Cancellation is not a `ProcessError`. When the caller cancels the enclosing Flow or ends stream consumption early, the
processes are terminated and the Flow ends with `Cause.Interrupt`. The caller that requested the cancellation decides
what it means by matching the `Exit`.

## Match the error when the caller needs a policy

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
let explain (exit: Exit<ProcessResult, ProcessError>) =
    match exit with
    | Exit.Failure(Cause.Fail(ProcessError.StartFailed failure)) ->
        $"could not start {failure.Command}"
    | Exit.Failure(Cause.Fail(ProcessError.TimedOut failure)) ->
        $"{failure.Specification} exceeded {failure.Timeout}"
    | Exit.Failure(Cause.Fail(ProcessError.StageFailed failure)) ->
        $"stage {failure.Stage.Stage} exited {failure.Stage.ExitCode}"
    | _ -> "ok"
```

```fsharp run
Process.command $"sh -c 'exit 3'" |> Process.toFlow |> Flow.run host |> explain |> shouldEqual "stage 0 exited 3"
Process.command $"no-such-command-axial-docs" |> Process.toFlow |> Flow.run host |> explain |> shouldEqual "could not start no-such-command-axial-docs"
```

[`ProcessError.describe`](xref:M:Axial.Process.ProcessError.describe) formats a redacted diagnostic.
[`ProcessError.exitCode`](xref:M:Axial.Process.ProcessError.exitCode) maps a failed stage to its native exit code, timeout
to 124, cancellation to 130, and startup or I/O failure to 1. The host [`run`](xref:M:Axial.Process.DSL.run) applies the
same mapping automatically.

## What a successful result contains

A `ProcessResult` is returned only when every stage satisfies its success policy. It contains the final stdout and
stderr text, exact captured bytes, every stage's exit code, start times, durations, and bounded stderr tails. Change
[`Process.successCodes`](xref:M:Axial.Process.Process.successCodes) when a command treats other exit codes as successful.

```fsharp
let stageSummary (specification: ProcessSpec) : Flow<ProcessHostEnvironment, ProcessError, string list> =
    flow {
        let! result = specification |> Process.toFlow
        return [ for stage in result.Stages -> $"[{stage.Stage}] {stage.Command} => {stage.ExitCode}" ]
    }
```

```fsharp run
Process.command $"printf 'a\nb\n'" |> Process.pipe (Process.command $"wc -l")
|> stageSummary
|> Flow.run host
|> shouldEqual (Exit.Success [ "[0] printf 'a\nb\n' => 0"; "[1] wc -l => 0" ])
```
