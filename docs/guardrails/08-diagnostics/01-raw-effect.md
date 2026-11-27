---
title: "raw-effect (AXG001): use explicit effect services"
linkTitle: raw-effect
description: Reports direct calls to the clock, files, randomness and other effects that a test cannot replace.
---

# raw-effect (AXG001)

## The problem

A function that calls `File.ReadAllText` reads the disk in every test, and its signature does not say so. A function
that calls `Random()` gives different answers each run, so a failing test may not fail again. Axial gives each of
these effects a service that travels in the flow. Tests swap the service; the signature shows the dependency.

```fsharp
open System.IO

/// Reported: reads the real disk; a test cannot supply the file.
let loadSettings (path: string) = File.ReadAllText path
```

```fsharp
open Axial.FileSystem

/// Fixed: the file system comes from the environment; a test passes an in-memory one.
let loadSettingsFromService (path: string) = FileSystem.readAllText path
```

The finding names the service, the environment field to add, the live implementation and the test fake.
`axial capabilities` lists every service and the APIs it replaces.

## What it reports

| Category | Reports | Use instead |
| --- | --- | --- |
| `random` | `new System.Random` | `IRandom`, `Random.nextDouble` |
| `guid` | `Guid.NewGuid()` | `IGuid`, `Guid.newGuid` |
| `clock` | Date and time properties, `Stopwatch`, `Task.Delay` | `IClock`, `Clock.now`, `Clock.timed`, `Flow.sleep`, `Schedule` |
| `environment` | Environment variables, machine and process properties, `TimeZoneInfo.Local` | `IEnvironment` or a value in `'env`; an explicit `TimeZoneInfo`; `Parallelism.ofProcessors` and `Parallelism.count` for `ProcessorCount` |
| `console` | Any `System.Console` member | `Axial.Console.IConsole` |
| `filesystem` | Any `System.IO.File` or `System.IO.Directory` member | `Axial.FileSystem.IFileSystem` |
| `process` | `Process.Start` | `Axial.Process.IProcess` |
| `sleep` | `Thread.Sleep` | `Flow.sleep`, `Schedule` |
| `http` | `new HttpClient` | `Axial.HttpClient.IHttp` with `Http.live` over one shared client |
| `blocking` | `Async.RunSynchronously`, `Task.Wait()`, `.Result`, `.GetAwaiter().GetResult()`, and blocking waits such as `SemaphoreSlim.Wait` | `let!` inside a flow; await outside one |
| `scope` | `Cache.make*`, `Hub.make`, `Queue.bounded` returned by a run | Use the service before its scope closes |

It matches resolved `System.*` symbols, not text: your own type named `Random` is not reported.

Two cases are allowed:

- Reading `.Result` when the code just checked the task is complete, in the enclosing `if` or `when`, or inside a
  `ContinueWith` callback.
- `TimeZoneInfo.ConvertTimeFromUtc(utc, zone)` with a zone you pass in. Reading `TimeZoneInfo.Local` to get that zone
  is reported.

## Where raw calls belong

A file that implements a service, or tests it against the real thing, claims the capability on its `Compile` item and
may then call that capability's API:

```xml
<Compile Include="Live/Stripe.fs" AxialCapability="payments" />
```

Host files may make any raw call. Elsewhere, when no service fits yet, one call can carry an allowance:

```fsharp
let pipe = new System.IO.Pipes.NamedPipeServerStream("app-control") // axial-allow-raw-effect: Axial.Process has no named pipes yet (gap: process-named-pipes)
```

See [The policy file](../policy-file.html), [Capabilities](../capabilities.html) and
[Allowances](../allowances-and-notes.html).
