# Process identity and graceful stop

`Process.onStarted` runs once for each stage immediately after the operating-system process starts and before Axial publishes any output from that stage. The `ProcessStarted` value contains the zero-based stage index, rendered command, start time, process ID, and a `Close` flow.

Keep the handle in application state when a supervisor needs to restart or stop a long-lived child. Sending a request after the stage exits succeeds without doing anything.

```fsharp
let supervise publish (command: ProcessCommand) =
    command
    |> Process.onStarted (fun started ->
        publish started
        Flow.succeed ())
    |> Process.toFlow
```

A completed `StageResult` also contains `ProcessId`, so batch and streaming executions retain the identity of every stage.

## Stop requests

`ProcessStarted.Close` accepts one `StopRequest`:

- `Interrupt` sends `SIGINT` on Unix.
- `Terminate` sends `SIGTERM` on Unix.
- `Hangup` sends `SIGHUP` on Unix.
- `Signal number` sends that numeric Unix signal.
- `CloseStdIn` closes stdin when Axial owns the redirected pipe.
- `CloseMainWindow` asks a Windows GUI process to close its main window.

A request the platform or stage cannot support fails with `ProcessError.StopUnsupported`, and a request that was attempted but failed fails with `ProcessError.StopFailed`. .NET's `ProcessStartInfo` cannot create the new Windows process group required to target `GenerateConsoleCtrlEvent` safely. Axial therefore never sends a console event to group `0`: `Interrupt`, `Terminate`, `Hangup`, and raw numeric signals return `StopUnsupported` on Windows; on Unix, `CloseMainWindow` returns `StopUnsupported`. A graceful-stop ladder skips unsupported steps, so it continues to its next step. `CloseMainWindow` is the Windows graceful-stop path.

## Escalation on interruption or timeout

By default, interruption and process timeout immediately kill the complete process tree. `Process.stopGracefully` installs an ordered escalation ladder. Axial sends each request to every live stage, waits at most that step's duration for all stages to exit, and proceeds to the next request. It kills the process tree after the final grace period when stages remain alive.

```fsharp
let withGracefulStop (command: ProcessCommand) =
    command
    |> Process.stopGracefully [
        StopRequest.CloseStdIn, TimeSpan.FromSeconds 1.
        StopRequest.Terminate, TimeSpan.FromSeconds 5.
    ]
    |> Process.toFlow
```

For the common single-step policy, use `[ StopRequest.Terminate, grace ]`. Cancellation remains `Cause.Interrupt`; stage and overall timeouts retain their existing typed errors after graceful shutdown has had its opportunity to run.

The ladder runs once per execution. If cancellation and a stage timeout both happen, the one observed first decides the result: a timeout recorded before cancellation keeps `StageTimedOut`, and otherwise the result is `Cause.Interrupt`. The later cause waits for the running ladder rather than killing early. `CloseStdIn` starts a short, fixed bound before requesting feeder cancellation, then closes the pipe, so a blocking cancellation callback or a child that never reads its input cannot hold the ladder at that step.

When a process exits, Axial asks a user `InputSource.Stream` flow to stop and gives it the same short bound. The process execution then returns even if that flow ignores interruption; Axial observes any eventual fault from the detached work so it does not become an unobserved task exception. A typed input failure or defect that settles before the bound still remains the execution's failure.

Axial.Process currently provides a live implementation only on .NET. The Fable build can type-check process specifications and test doubles, but it does not start Node child processes.
