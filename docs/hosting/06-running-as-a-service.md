---
title: Running as a service
linkTitle: Running as a service
description: Register a root Flow with the Microsoft Generic Host, notify systemd, and watchdog long-running work.
---

# Running as a service

`Axial.Hosting` integrates a root Flow with the Generic Host. Register it with `Hosting.addApp`; the watchdog reads the runtime clock and log sink, so the environment needs no clock or log member.

## Register with the Generic Host

Wire the root Flow to the host lifetime once. `Hosting.addApp` runs the workflow when the host starts and requests
`App.Stop()` on shutdown; `AddSystemd()` (or `AddWindowsService()` on Windows) selects the host lifetime. The
[.NET hosting guide](/hosting/dotnet.html) shows the full end-to-end composition, including building the application
environment from the container with a layer:

```fsharp no-check reason="Needs the Microsoft.Extensions.Hosting package, which the documentation build does not reference"
open Microsoft.Extensions.Hosting
open Axial
open Axial.Hosting

let builder = Host.CreateApplicationBuilder args

builder.Services.AddSystemd() |> ignore    // or AddWindowsService() on Windows
builder.Services
|> Hosting.addApp id AppError.describe application
|> ignore

use host = builder.Build()
host.Run()
```

## systemd

Call `AddSystemd()` on the `HostApplicationBuilder` (`UseSystemd()` on an older `IHostBuilder`). The extension comes from the `Microsoft.Extensions.Hosting.Systemd` package and installs a `SystemdLifetime` that sends the `READY=1` and `STOPPING=1` notifications the `Type=notify` unit waits for. The watchdog is a separate transport: construct it at the process edge with `SystemdNotify.live` and the `NOTIFY_SOCKET` value, and pass it to `Hosting.watchdog` or `Hosting.watchdogWith` inside the application Flow — never to the host builder. `Hosting.watchdog` detects a wedged Axial scheduler or starved thread pool; its sleeping heartbeat and always-true health check do **not** establish application progress. Supply `Hosting.watchdogWith variables processId notifier isHealthy` to tie pings to work your application owns. Record the monotonic time after each successful job and reject stale progress:

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
let progressWatchdog<'env>
    (clock: IClock) (variables: string -> string option) (processId: int) (notifier: ISystemdNotifier) =
    let mutable lastCompletedTicks: int64 = clock.Elapsed().Ticks
    // Call after each completed unit of work in the job loop.
    let recordCompletion () =
        Interlocked.Exchange(&lastCompletedTicks, clock.Elapsed().Ticks) |> ignore
    let isHealthy : Flow<'env, Never, bool> =
        Flow.delay (fun () ->
            Flow.succeed (clock.Elapsed().Ticks - Interlocked.Read(&lastCompletedTicks) < TimeSpan.FromSeconds(2.).Ticks))
    recordCompletion, Hosting.watchdogWith variables processId notifier isHealthy
```

`recordCompletion` is shared between the job loop and the watchdog: the loop calls it after each completed unit of
work, and `isHealthy` reads the same cell to decide whether progress is recent. Compose the loop in parallel with the
watchdog and register the result with `Hosting.addApp`:

```fsharp
open System
open System.Threading
open Axial
open Axial.Hosting
open Axial.PlatformService

// The job loop owns the work and records progress after each completed unit.
let jobLoop (recordCompletion: unit -> unit) : Flow<'env, Never, unit> =
    let rec loop () = flow {
        do! Flow.succeed () // one unit of work
        recordCompletion ()
        return! loop ()
    }
    loop ()

let application<'env>
    (clock: IClock) (variables: string -> string option) (processId: int) (notifier: ISystemdNotifier)
    : Flow<'env, Never, unit> =
    let recordCompletion, watchdog = progressWatchdog<'env> clock variables processId notifier
    Flow.zipPar (jobLoop recordCompletion) watchdog
    |> Flow.map ignore
```

```fsharp run
// The host can compose the watchdog without opening a systemd socket.
let variables (_: string) = None
let notifier = { new ISystemdNotifier with member _.Notify _ = () }
let recordCompletion, watchdog = progressWatchdog<unit> Clock.live variables 1 notifier
recordCompletion ()
(watchdog |> Flow.withLogSink LogSink.none |> Flow.run ()) |> shouldEqual (Exit.Success ())
```


The watchdog reads `WATCHDOG_USEC` and optional `WATCHDOG_PID`; it sends `WATCHDOG=1` at half the configured interval only when its runtime heartbeat advances and the health flow returns true. Invalid or unrepresentable intervals disable the watchdog and log a warning through the runtime log sink. For application work that must gate the heartbeat itself, use `Hosting.watchdogWithHeartbeat`. The watchdog uses the runtime's clock and log sink (`Flow.withClock`, `Flow.withLogSink`); the Generic Host installs the host's sink at the root.

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

`Type=notify` waits for the READY notification that `SystemdLifetime` (installed by `UseSystemd()`) sends when the host starts. `NotifyAccess=main` permits notifications from the main process; `Restart=always` recovers from a crash or watchdog timeout. Choose `WatchdogSec` with enough margin for the heartbeat and health check.

On Linux with a user systemd session, run `scripts/check-systemd-watchdog.sh` from the repository. It builds a transient probe, verifies steady operation, SIGKILL recovery and watchdog timeout, then stops and resets its units. This is a manual check, not part of CI.

## Windows

Call `AddWindowsService()` on the `HostApplicationBuilder` (`UseWindowsService()` on an older `IHostBuilder`) and register the root application with `Hosting.addApp`. Configure Service Control Manager recovery separately; registration alone does not configure restart policy:

```text
sc failure <svc> reset= 86400 actions= restart/5000/restart/5000/restart/5000
```
