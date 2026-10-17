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
| `StageTimedOut` | A command exceeds its per-stage timeout. | Stage index, redacted command, and timeout duration |
| `OverallTimedOut` | The whole topology exceeds `overallTimeout`, including callbacks and drain. | Redacted specification and timeout duration |
| `LineTooLong` | A decoded line exceeds `maxLineLength`. | Stage, original channel, and UTF-16 limit |
| `ObservationUnavailable` | A requested output channel uses inherited handles. | Stage and channel |
| `StageFailed` | A started command exits with a code outside its configured success-code set. This is the usual result for a command that reports failure with a nonzero exit code. | Failed `StageResult` and the complete `ProcessResult` |
| `IoFailed` | Axial cannot read, write, or route one of the process streams or configured file targets, or the optional drain deadline expires. | I/O error message |

When a pump fails, Axial cancels the remaining I/O and attempts to terminate running stages, preserving the original
failure. A pipeline consumer that exits before accepting all input can therefore produce `IoFailed` for the broken
pipe. Timeout, caller cancellation, and partial startup also trigger termination. Termination is best effort: modern
.NET targets request tree termination, while the `netstandard2.1` implementation kills direct stages only. Parent
crashes bypass this cleanup, and descendants that outlive their direct parent may remain running.

Cancellation is not a `ProcessError`. When the caller cancels the enclosing Flow, the
processes are terminated and the Flow ends with `Cause.Interrupt`. `ProcessStream.take` and `takeWhile` own early
stopping and instead return successful `StoppedEarly` completion. The caller that requested the cancellation decides
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
open Axial.Process.DSL

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
let explain (exit: Exit<ProcessResult, ProcessError>) =
    match exit with
    | Exit.Failure(Cause.Fail(ProcessError.StartFailed failure)) ->
        $"could not start {failure.Command}"
    | Exit.Failure(Cause.Fail(ProcessError.StageTimedOut failure)) ->
        $"{failure.Command} exceeded {failure.Timeout}"
    | Exit.Failure(Cause.Fail(ProcessError.StageFailed failure)) ->
        $"stage {failure.Stage.Stage} exited {failure.Stage.ExitCode}"
    | _ -> "ok"
```

```fsharp run
cmd $"sh -c 'exit 3'" |> Process.tryRun |> explain |> shouldEqual "stage 0 exited 3"
cmd $"no-such-command-axial-docs"
|> Process.tryRun
|> explain
|> shouldEqual "could not start no-such-command-axial-docs"
```

[`ProcessError.describe`](xref:M:Axial.Process.ProcessError.describe) formats a redacted diagnostic.
[`ProcessError.exitCode`](xref:M:Axial.Process.ProcessError.exitCode) maps a failed stage to its native exit code, timeout
to 124, and startup or I/O failure to 1. Cancellation remains `Cause.Interrupt`; an entry point
can match that cause and return 1. `Process.tryRun` leaves this policy with the caller.

## What a successful result contains

A `ProcessResult` is returned only when every stage satisfies its success policy. It contains the final stdout and
stderr text, exact captured bytes, every stage's exit code, start times, durations, and bounded stderr tails.
Durations use the injected monotonic clock: stage durations stop at native exit, while the result duration includes
output drain and awaited callbacks. A failure result contains the capture available before cleanup. Change
[`Process.successCodes`](xref:M:Axial.Process.Process.successCodes) when a command treats other exit codes as successful.

```fsharp
let stageSummary (specification: ProcessSpec) =
    flow {
        let! result = specification |> toFlow
        return
            [ for stage in result.Stages ->
                  $"[{stage.Stage}] {stage.Command} => {stage.ExitCode}" ]
    }
```

```fsharp run
cmd $"printf 'a\nb\n'" |> pipeTo (cmd $"wc -l")
|> stageSummary
|> Process.run
|> shouldEqual [ "[0] printf \"a\nb\n\" => 0"; "[1] wc -l => 0" ]
```

## Limit draining after process exit

A descendant can retain inherited stdout or stderr handles after its parent exits. Axial drains to EOF by default so
late output is preserved. Configure [`Process.drainTimeout`](xref:M:Axial.Process.Process.drainTimeout) when waiting
indefinitely is unsuitable:

```fsharp
let boundedDrain =
    cmd $"printf ready"
    |> drainTimeout (TimeSpan.FromSeconds 2.0)
    |> toFlow
```

```fsharp run
boundedDrain |> Process.tryRun |> function
| Exit.Success result -> shouldEqual "ready" result.StdOut
| other -> failwithf "Expected successful drain, got %A" other
```

The drain deadline starts after every directly managed stage exits and covers remaining input, output, and sink
completion. It uses the clock supplied to `Process.live`. Expiry cancels remaining I/O and returns `IoFailed`;
partial captured output is not returned as a successful result. This does not terminate descendants whose direct
parent has already exited. Destinations must cooperate with cancellation for draining to stop promptly.

If a downstream stage is still waiting for EOF, the drain deadline has not started. Use `Process.overallTimeout` to limit
the complete execution, including running stages, callbacks, and draining. `Process.timeout` limits one command
from successful launch to its native exit. The DSL also exposes `drainTimeout`. Both policies
are opt-in and appear in `Process.plan`.

## Choose stage and overall deadlines

Apply `timeout` before connecting a command; it measures that stage through native exit. `overallTimeout`
applies to the whole topology, including draining and completion-aware callbacks. Both remain typed failures.

```fsharp run
let slowStage = sh $"sleep 30" |> timeout (TimeSpan.FromMilliseconds 100.) |> toFlow
match slowStage |> Process.tryRun with
| Exit.Failure(Cause.Fail(ProcessError.StageTimedOut failure)) -> shouldEqual 0 failure.Stage
| other -> failwithf "Expected stage timeout, got %A" other

let slowTopology =
    sh $"sleep 30"
    => cmd $"cat"
    |> overallTimeout (TimeSpan.FromMilliseconds 100.)
    |> toFlow
match slowTopology |> Process.tryRun with
| Exit.Failure(Cause.Fail(ProcessError.OverallTimedOut _)) -> ()
| other -> failwithf "Expected overall timeout, got %A" other
```
