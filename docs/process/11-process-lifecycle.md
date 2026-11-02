# Process identity and graceful stop

`Process.onStarted` runs once for each stage immediately after the operating-system process starts and before Axial publishes any output from that stage. The `ProcessStarted` value contains the zero-based stage index, rendered command, start time, process ID, and `Close` and `Signal` flows.

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

## Coming from System.Diagnostics.Process

`ProcessStarted.Close` differs from .NET's `Process.CloseMainWindow` / `Process.Kill` and from sending one signal: it owns whole-execution shutdown and escalates after configured grace periods, ultimately killing the process tree. Without a stop policy, it escalates immediately. Use `ProcessStarted.Signal` for a single request with no escalation.

## Stop requests

`ProcessStarted.Signal` sends exactly one stage-local `StopRequest`, without taking ownership of shutdown. `ProcessStarted.Close` sends an initial request and, if it succeeds, starts coordinated shutdown of the entire execution. Both use the same mapping:

- `Interrupt` sends `SIGINT` on Unix.
- `Terminate` sends `SIGTERM` on Unix.
- `Hangup` sends `SIGHUP` on Unix.
- `Signal number` sends that numeric Unix signal.
- `CloseStdIn` closes stdin when Axial owns the redirected pipe.
- `CloseMainWindow` asks a Windows GUI process to close its main window.

A request the platform or stage cannot support fails with `ProcessError.StopUnsupported`, and a request that was attempted but failed fails with `ProcessError.StopFailed`. .NET's `ProcessStartInfo` cannot create the new Windows process group required to target `GenerateConsoleCtrlEvent` safely. Axial therefore never sends a console event to group `0`: `Interrupt`, `Terminate`, `Hangup`, and raw numeric signals return `StopUnsupported` on Windows; on Unix, `CloseMainWindow` returns `StopUnsupported`. A graceful-stop ladder skips unsupported steps, so it continues to its next step. `CloseMainWindow` is the Windows graceful-stop path.

The returned flow acknowledges the initial request; it does not wait for shutdown or native reap. Await the process execution for that. After shutdown starts, later `Close` requests succeed without sending another request or waiting for the active ladder, including when timeout started shutdown. Unsupported or failed initial requests do not start shutdown. A handle used after its stage exits is a no-op, even if other stages remain live.

`Signal` returns after delivery, not process exit. It neither starts nor restarts nor cancels an active ladder; while a ladder is running it still sends the requested action to the live stage. Its grace deadlines and first stop cause remain unchanged. A request after stage exit is a successful no-op. Unsupported and failed requests use the same typed errors as `Close`. Output observation continues after a standalone `Signal`, but an already-running ladder still stops observation.

Both pipeline and DSL users obtain these flows through `onStarted`; no separate signal DSL operator is needed.

```fsharp
let halfClose (started: ProcessStarted) =
    started.Signal StopRequest.CloseStdIn
```

## Escalation on Close, interruption or timeout

By default, an accepted `Close`, interruption and process timeout immediately kill the complete process tree. `Process.stopGracefully` installs an ordered escalation ladder. Axial sends each request to every live stage, waits at most that step's duration for all stages to exit, and proceeds to the next request. It kills the process tree after the final grace period when stages remain alive.

```fsharp
let withGracefulStop (command: ProcessCommand) =
    command
    |> Process.stopGracefully [
        StopRequest.CloseStdIn, TimeSpan.FromSeconds 1.
        StopRequest.Terminate, TimeSpan.FromSeconds 5.
    ]
    |> Process.toFlow
```

For the common single-step policy, use `[ StopRequest.Terminate, grace ]`. When `Close StopRequest.Terminate` starts that policy, Axial reuses the initial signal for the addressed stage rather than sending it twice; other live stages receive the same first step. A different initial request precedes the configured ladder. All grace periods use the clock captured by the running execution, not the clock of the caller submitting `Close`.

`Close` is not a standalone signal or stdin half-close. Use `Signal StopRequest.CloseStdIn` to deliver EOF without a shutdown deadline, or `Signal StopRequest.Terminate` for one TERM without later escalation. To allow EOF to finish the child before owned escalation, configure `[ StopRequest.CloseStdIn, grace ]` and use `Close`. Application-protocol shutdown messages still go through the input source.

Cancellation remains `Cause.Interrupt`. A stage or overall timeout recorded first retains its typed error after graceful shutdown. When external `Close` is recorded first, the run reports its ordinary exit-code result (`StageFailed` for an unaccepted exit code), not a later timeout. Output observation stops when shutdown starts; do not use output callbacks to acknowledge subsequent signals.

Every execution starts the coordinator, including a single command without optional policies, so caller interruption and output-destination failure always have a live stop path. The ladder runs once per execution, including when `Close` races a timeout. If cancellation and a stage timeout both happen, the one observed first decides the result: a timeout recorded before cancellation keeps `StageTimedOut`, and otherwise the result is `Cause.Interrupt`. The later cause waits for the running ladder rather than killing early. Output-cap failures, broken pipeline destinations, and failed output callbacks also stop the remaining stages instead of waiting for their natural exit; the original output or destination failure remains the result.

`CloseStdIn` requests feeder cancellation and closes the pipe without waiting, so a blocking cancellation callback or a child that never reads its input cannot hold the ladder at that step.

A user `InputSource.Stream` is consumed by the stdin feeder fiber; only each stream write crosses the BCL task bridge. When a process exits or stdin is closed, Axial asks that fiber to stop and closes the pipe, which also releases a write blocked on a full pipe. The process execution then waits for the feeder, with no time limit, before it returns. A typed input failure or defect the feeder settles with is the execution's failure, including one raised while it stops; interruption and the pipe-closed errors caused by closing the pipe are not failures.

A source that ignores interruption keeps the execution waiting. To let the execution return without it, wrap that source in `Flow.disconnect`: the execution returns, the source keeps settling in its own fiber owned by the application, and its eventual defect is reported once to the fiber observer.

Axial.Process currently provides a live implementation only on .NET. The Fable build can type-check process specifications and test doubles, but it does not start Node child processes.
