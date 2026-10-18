# Running as a service

`Axial.Hosting` integrates a root Flow with the Generic Host. Register it with `Hosting.addApp`; supply an environment containing `IHasClock` for the watchdog.

## systemd

Call `UseSystemd()` on the Generic Host builder. Run the root application alongside `Hosting.watchdogWith variables processId notifier isHealthy` (or `Hosting.watchdog` without a custom health check). Construct the notifier at the process edge with `SystemdNotify.live` and the `NOTIFY_SOCKET` environment variable. The watchdog reads `WATCHDOG_USEC` and optional `WATCHDOG_PID`; it sends `WATCHDOG=1` at half the configured interval only when its runtime heartbeat has advanced and the health flow returns true. For application work that can stall independently, use `Hosting.watchdogWithHeartbeat` with a gate driven by that work; a healthy timer alone cannot prove the application is making progress.

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
