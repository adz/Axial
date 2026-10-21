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

A request that the platform or stage cannot support fails with `ProcessError.StopFailed`. Windows console control events are not currently available through `Close`; `Interrupt`, `Terminate`, `Hangup`, and raw numeric signals therefore return `StopFailed` on Windows.

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

For the common single-step policy, use `[ StopRequest.Terminate, grace ]`. Cancellation remains `Cause.Interrupt`; an overall timeout remains `ProcessError.OverallTimedOut` after graceful shutdown has had its opportunity to run.
