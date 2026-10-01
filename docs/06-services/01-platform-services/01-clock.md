---
title: Clock
description: Read the current instant through an explicit service.
---

`IClock` provides `UtcNow()` for timestamps, `Elapsed()` for monotonic durations, and `Sleep()` for cancellable delays. Timed flows require `IHasClock` in their environment, so a host or test supplies one source for all three operations.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
open System
open Axial
open Axial.PlatformService
```

```fsharp
let expiresWithin (window: TimeSpan) (expiry: DateTimeOffset) : Flow<#IHasClock, Never, bool> =
    Clock.now |> Flow.map (fun now -> expiry - now <= window)
```

`Clock.now` returns a `DateTimeOffset`. The other readers derive from it. Here they run against the base runtime
with its clock fixed at noon UTC on 1 January 2026:

```fsharp transcript
> open System;;
> open Axial.PlatformService;;
> let runtime () = { BaseRuntime.liveValue with Clock = Clock.fromValue (DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)) };;
> (Clock.now : Flow<BaseRuntime, Never, DateTimeOffset>) |> Flow.map _.Hour |> Flow.run (runtime ());;
val it: Exit<int,Never> = Success 12

> (Clock.utcDateTime : Flow<BaseRuntime, Never, DateTime>) |> Flow.map _.Kind |> Flow.run (runtime ());;
val it: Exit<DateTimeKind,Never> = Success Utc

> (Clock.unixTimeSeconds : Flow<BaseRuntime, Never, int64>) |> Flow.run (runtime ());;
val it: Exit<int64,Never> = Success 1767268800L

> (Clock.unixTimeMilliseconds : Flow<BaseRuntime, Never, int64>) |> Flow.run (runtime ());;
val it: Exit<int64,Never> = Success 1767268800000L
```

None of them produce a typed failure, so the error channel stays free for the workflow's own errors.

## Measuring durations

Wall-clock time can jump when the system clock is adjusted, so it is the wrong tool for measuring how long something
took. `IClock.Elapsed` is a monotonic reading for that job, and `Clock.timed` wraps a flow with it:

```fsharp
let buildReport : Flow<BaseRuntime, Never, string> = Flow.ok "report"

let timedReport : Flow<BaseRuntime, Never, string> =
    flow {
        let! report, took = buildReport |> Clock.timed
        do! Log.info $"report built in {took.TotalMilliseconds:F0} ms"
        return report
    }
```

`Clock.elapsed` reads the timer directly; only the difference between two readings is meaningful. Measuring through
the clock instead of `Stopwatch` keeps durations deterministic in tests: under `Clock.fromValue` every duration is
zero:

```fsharp run
let logged = ResizeArray<string>()

let testRuntime =
    { BaseRuntime.liveValue with
        Clock = Clock.fromValue (DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero))
        Log = Log.fromSink (fun _ message -> logged.Add message) }

timedReport |> Flow.run testRuntime |> shouldEqual (Exit.Success "report")
List.ofSeq logged |> shouldEqual [ "report built in 0 ms" ]
```

## Supplying the service

`Clock.live` reads `DateTimeOffset.UtcNow` and a monotonic process timer. `Clock.layer` is the same value as a `Layer<unit, Never, IClock>`. Most
applications get the clock as part of [the base runtime](index.html) rather than wiring it alone.

## Testing

`Clock.fromValue` pins the instant for read-only tests. It deliberately rejects `Sleep`; use `ManualClock` for a workflow that sleeps, times out, retries, or forks fibers. Advance it after the expected sleeper has registered:

```fsharp
let clock = ManualClock(DateTimeOffset.Parse "2026-01-01T00:00:00Z")
let env = ClockEnvironment(clock :> IClock)
let sleeping : Flow<ClockEnvironment, Never, unit> = Flow.sleep (TimeSpan.FromSeconds 5.0)
let pending = sleeping |> Flow.toAsync env |> Async.StartAsTask
// Wait until clock.Sleepers reports one waiting operation.
clock.AdvanceBy(TimeSpan.FromSeconds 5.0)
```

`ManualClock` is currently available on .NET. `Clock.live` supports .NET and Fable. A fixed clock is enough for a time-read assertion:

On Fable, an F# record may use a field named `Clock` and implement `IHasClock.Clock`. Axial marks the interface for
Fable name mangling so the generated JavaScript does not confuse the record field with the interface getter. If
handwritten JavaScript supplies an environment, construct an Axial environment in F# (or use the generated
`ClockEnvironment` constructor) rather than passing a plain `{ Clock: clock }` object. The latter lacks the interface
method the compiled Flow calls. This rule also applies to the other `IHas*` service interfaces.

```fsharp
type ClockEnv =
    { Clock: IClock }
    interface IHasClock with
        member this.Clock = this.Clock

let atNoon = { Clock = Clock.fromValue (DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)) }
```

```fsharp run
expiresWithin (TimeSpan.FromHours 1.0) (DateTimeOffset(2026, 1, 1, 12, 30, 0, TimeSpan.Zero))
|> Flow.run atNoon
|> shouldEqual (Exit.Success true)

expiresWithin (TimeSpan.FromHours 1.0) (DateTimeOffset(2026, 1, 1, 15, 0, 0, TimeSpan.Zero))
|> Flow.run atNoon
|> shouldEqual (Exit.Success false)
```

A clock that returns a fixed instant does not advance, which is usually what you want for assertions. When a test
needs time to move, supply an `IClock` closing over a mutable field and step it explicitly, so the test controls
how time moves instead of depending on wall-clock timing.

Note that `IClock` reports the time; it does not schedule. Delays, timeouts, and retry policies are Flow runtime
concerns; see [scheduling and retries](/scheduling-and-retries/index.html).
