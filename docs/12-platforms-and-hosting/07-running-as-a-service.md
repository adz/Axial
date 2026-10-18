# Running as a service

`Axial.Hosting` integrates a root Flow with the Generic Host. Register it with `Hosting.addApp`; supply an environment containing `IHasClock` for the watchdog.

## systemd

Call `UseSystemd()` on the Generic Host builder. Construct the notifier at the process edge with `SystemdNotify.live` and `NOTIFY_SOCKET`. `Hosting.watchdog` detects a wedged Axial scheduler or starved thread pool; its sleeping heartbeat and always-true health check do **not** establish application progress. Supply `Hosting.watchdogWith variables processId notifier isHealthy` to tie pings to work your application owns. For example, record the monotonic time after each successful job and reject stale progress:

```fsharp
// no-check: host wiring and application environment are supplied by the service.
let mutable lastCompletedTicks = clock.Elapsed().Ticks
// In the job loop, after a completed unit of work:
System.Threading.Interlocked.Exchange(&lastCompletedTicks, clock.Elapsed().Ticks) |> ignore
let isHealthy : Flow<ProbeEnv, Never, bool> =
    Flow.delay (fun () -> Flow.succeed (clock.Elapsed().Ticks - System.Threading.Interlocked.Read(&lastCompletedTicks) < TimeSpan.FromSeconds(2.).Ticks))
let watchdog = Hosting.watchdogWith variables processId notifier isHealthy
```

The watchdog reads `WATCHDOG_USEC` and optional `WATCHDOG_PID`; it sends `WATCHDOG=1` at half the configured interval only when its runtime heartbeat advances and the health flow returns true. Invalid or unrepresentable intervals disable the watchdog and log a warning through the environment's `IHasLog`. For application work that must gate the heartbeat itself, use `Hosting.watchdogWithHeartbeat`. All watchdog environments supply `IHasClock` and `IHasLog`.

```ini
[Unit]
Description=Axial agent

[Service]
Type=notify
ExecStart=/usr/bin/dotnet /opt/axial/Agent.dll
WatchdogSec=3
Restart=always
RestartSec=1
NotifyAccess=main

[Install]
WantedBy=default.target
```

`Type=notify` waits for the host's READY notification. `NotifyAccess=main` permits notifications from the main process; `Restart=always` recovers from a crash or watchdog timeout. Choose `WatchdogSec` with enough margin for the heartbeat and health check.

On Linux with a user systemd session, run `scripts/check-systemd-watchdog.sh` from the repository. It builds a transient probe, verifies steady operation, SIGKILL recovery and watchdog timeout, then stops and resets its units. This is a manual check, not part of CI.

## Windows

Call `UseWindowsService()` on the Generic Host builder and register the root application with `Hosting.addApp`. Configure Service Control Manager recovery separately; registration alone does not configure restart policy:

```text
sc failure <svc> reset= 86400 actions= restart/5000/restart/5000/restart/5000
```
