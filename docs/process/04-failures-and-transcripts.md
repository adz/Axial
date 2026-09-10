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
| `Canceled` | The caller cancels the enclosing Flow or ends stream consumption early. | Cancellation message |
| `StageFailed` | A started command exits with a code outside its configured success-code set. This is the usual result for a command that reports failure with a nonzero exit code. | Failed `StageResult` and the complete `ProcessResult` |
| `IoFailed` | Axial cannot read, write, or route one of the process streams or configured file targets. | I/O error message |

For every timeout, cancellation, and partial pipeline startup, Axial terminates stages that did start before the Flow
finishes. The typed error tells the caller what happened after cleanup, rather than leaving a child process running in
the background.

## Match the error when the caller needs a policy

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
match exit with
| Exit.Failure(Cause.Fail(ProcessError.StartFailed failure)) ->
    eprintfn "could not start %s: %s" failure.Command failure.Message
| Exit.Failure(Cause.Fail(ProcessError.TimedOut failure)) ->
    eprintfn "%s exceeded %O" failure.Specification failure.Timeout
| Exit.Failure(Cause.Fail(ProcessError.StageFailed failure)) ->
    eprintfn "stage %d exited %d" failure.Stage.Stage failure.Stage.ExitCode
| _ -> ()
```

[`ProcessError.describe`](xref:M:Axial.Process.ProcessError.describe) formats a redacted diagnostic.
[`ProcessError.exitCode`](xref:M:Axial.Process.ProcessError.exitCode) maps a failed stage to its native exit code, timeout
to 124, cancellation to 130, and startup or I/O failure to 1. The host [`run`](xref:M:Axial.Process.DSL.run) applies the
same mapping automatically.

## What a successful result contains

A `ProcessResult` is returned only when every stage satisfies its success policy. It contains the final stdout and
stderr text, exact captured bytes, every stage's exit code, start times, durations, and bounded stderr tails. Change
[`Process.successCodes`](xref:M:Axial.Process.Process.successCodes) when a command treats other exit codes as successful.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let! result = specification |> Process.toFlow

for stage in result.Stages do
    printfn "[%d] %s => %d (%O)" stage.Stage stage.Command stage.ExitCode stage.Duration
```
