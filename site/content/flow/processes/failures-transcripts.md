---
weight: 30
title: Failures And Transcripts
description: Interpret exit codes and diagnose the exact failed stage.
---

# Failures And Transcripts

This page shows how process failures retain structured context for CI, deployments, and support diagnostics.

## Pipefail Is The Default

`capture` and `toFlow` require every stage's configured success policy. If stage one fails in a three-stage pipeline,
the typed error is `ProcessError.StageFailed(stage, result)`, even when the final stage exits zero.

```fsharp
match exit with
| Exit.Failure(Cause.Fail(ProcessError.StageFailed(stage, transcript))) ->
    eprintfn "stage %d failed: %s" stage.Stage stage.Command
    eprintfn "exit: %d" stage.ExitCode
    eprintf "%s" stage.StdErrTail.Text
| _ -> ()
```

Each stage contains its redacted command, exit code, success decision, start time, duration, and bounded 64 KiB stderr
tail. The overall transcript contains configured captures, all exit codes, and total timing.

## Meaningful Non-Zero Codes

```fsharp
let grep =
    Process.command "grep" [ pattern; file ]
    |> Process.successCodes [ 0; 1 ]
```

Use `captureResult` or `Process.toFlowResult` when exit codes should remain data. Startup, cancellation, and I/O failures
still use `ProcessError`.

## Transcript Shape

```fsharp
let! result = pipeline |> toFlow

printfn "exit codes: %A" result.ExitCodes
printfn "duration: %O" result.Duration

for stage in result.Stages do
    printfn "[%d] %s => %d (%O)" stage.Stage stage.Command stage.ExitCode stage.Duration
```

This replaces Bash `PIPESTATUS`, manual timers, and ad hoc stderr buffering with one testable value.
