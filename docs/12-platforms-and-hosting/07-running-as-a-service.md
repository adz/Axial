# Running as a service

`Axial.Hosting` integrates a root Flow with the Generic Host. Register it with `Hosting.addApp`; supply an environment containing `IHasClock` for the watchdog.

## systemd

Call `UseSystemd()` on the Generic Host builder. Construct the notifier at the process edge with `SystemdNotify.live` and `NOTIFY_SOCKET`. `Hosting.watchdog` detects a wedged Axial scheduler or starved thread pool; its sleeping heartbeat and always-true health check do **not** establish application progress. Supply `Hosting.watchdogWith variables processId notifier isHealthy` to tie pings to work your application owns. For example, record the monotonic time after each successful job and reject stale progress:

```fsharp prepare
open System
open System.Threading
open Axial
open Axial.Hosting
open Axial.PlatformService

let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
open System
open System.Threading
open Axial
open Axial.Hosting
open Axial.PlatformService

// The host supplies these services at composition time.
let progressWatchdog<'env when 'env :> IHasClock and 'env :> IHasLog>
    (clock: IClock) (variables: IEnvironmentVariables) (processId: int) (notifier: ISystemdNotifier) =
    let mutable lastCompletedTicks: int64 = clock.Elapsed().Ticks
    // Call after each completed unit of work in the job loop.
    let recordCompletion () =
        Interlocked.Exchange(&lastCompletedTicks, clock.Elapsed().Ticks) |> ignore
    let isHealthy : Flow<'env, Never, bool> =
        Flow.delay (fun () ->
            Flow.succeed (clock.Elapsed().Ticks - Interlocked.Read(&lastCompletedTicks) < TimeSpan.FromSeconds(2.).Ticks))
    recordCompletion, Hosting.watchdogWith variables processId notifier isHealthy
```

```fsharp run
// The host can compose the watchdog without opening a systemd socket.
type ProbeEnv(clock: IClock, log: ILog) =
    interface IHasClock with member _.Clock = clock
    interface IHasLog with member _.Log = log
let variables =
    { new IEnvironmentVariables with
        member _.TryGet _ = None
        member _.Set(_, _) = ()
        member _.Expand text = text
        member _.GetAll() = System.Collections.Generic.Dictionary<string, string>() }
let notifier = { new ISystemdNotifier with member _.Notify _ = () }
let recordCompletion, watchdog = progressWatchdog<ProbeEnv> Clock.live variables 1 notifier
recordCompletion ()
let log =
    { new ILog with
        member _.Log _ _ = ()
        member _.LogException _ _ _ = () }
(watchdog |> Flow.run (ProbeEnv(Clock.live, log))) |> shouldEqual (Exit.Success ())
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
