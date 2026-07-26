---
title: Failures and transcripts
description: Handle structured process failures and inspect complete execution results.
weight: 30
type: docs
---

`Process.run` fails when startup, timeout, cancellation, I/O, or a stage success policy fails:

```fsharp
match exit with
| Exit.Failure(Cause.Fail(ProcessError.StartFailed failure)) ->
    eprintfn "could not start %s: %s" failure.Command failure.Message
| Exit.Failure(Cause.Fail(ProcessError.TimedOut failure)) ->
    eprintfn "%s exceeded %O" failure.Specification failure.Timeout
| Exit.Failure(Cause.Fail(ProcessError.StageFailed failure)) ->
    eprintfn "stage %d exited %d" failure.Stage.Stage failure.Stage.ExitCode
| _ -> ()
```

`ProcessError.describe` formats a redacted diagnostic. `ProcessError.exitCode` maps stage failure to its native exit code, timeout to 124, cancellation to 130, and other failures to 1.

Successful execution returns `ProcessResult`, including exact captured bytes, decoded text, every stage exit code, start times, durations, and bounded stderr tails. A configured `successCodes` set determines whether each stage succeeds.

```fsharp
let! result = specification |> Process.run

for stage in result.Stages do
    printfn "[%d] %s => %d (%O)" stage.Stage stage.Command stage.ExitCode stage.Duration
```

Timeout is specification policy. The Flow runtime races the execution, interrupts the losing workflow, waits for native cleanup, and then returns `ProcessError.TimedOut`. Caller cancellation follows the same tree-termination path and returns `ProcessError.Canceled` from the process interpreter.
