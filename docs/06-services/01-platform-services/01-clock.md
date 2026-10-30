---
title: Clock
description: Runtime time reads and explicit manual stepping in tests.
---

`IClock` supplies `UtcNow()`, monotonic `Elapsed()` and cancellable `Sleep()`. `Clock.now` and timed Flow operations
read the clock installed in the current runtime scope. An application environment does not need a clock field or
interface. `Clock.live` is the default; `Flow.withClock clock` overrides it for a flow and its descendants.

```fsharp prepare
open System
open Axial
open Axial.PlatformService
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
let expiresWithin (window: TimeSpan) (expiry: DateTimeOffset) : Flow<'env, Never, bool> =
    Clock.now |> Flow.map (fun now -> expiry - now <= window)
```

## Read a chosen instant

The `fixed` name needs double-backtick escaping in F# because it is a keyword:

```fsharp transcript
> open System;;
> open Axial;;
> open Axial.PlatformService;;
> let atNoon () = Clock.``fixed`` (DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));;
> (Clock.now : Flow<unit, Never, DateTimeOffset>) |> Flow.map _.Hour |> Flow.withClock (atNoon ()) |> Flow.run ();;
val it: Exit<int,Never> = Success 12

> (Clock.utcDateTime : Flow<unit, Never, DateTime>) |> Flow.map _.Kind |> Flow.withClock (atNoon ()) |> Flow.run ();;
val it: Exit<DateTimeKind,Never> = Success Utc

> (Clock.unixTimeSeconds : Flow<unit, Never, int64>) |> Flow.withClock (atNoon ()) |> Flow.run ();;
val it: Exit<int64,Never> = Success 1767268800L

> (Clock.unixTimeMilliseconds : Flow<unit, Never, int64>) |> Flow.withClock (atNoon ()) |> Flow.run ();;
val it: Exit<int64,Never> = Success 1767268800000L
```

These readers do not introduce a typed error. `Clock.service` returns the installed `IClock` when an interop
boundary needs the service itself.

## Measure durations

Use `Clock.elapsed` differences or `Clock.timed`, not wall-clock subtraction: UTC can jump when adjusted.

```fsharp
let timedReport : Flow<unit, Never, string> =
    flow {
        let! report, took = Flow.ok "report" |> Clock.timed
        do! Log.info $"report built in {took.TotalMilliseconds:F0} ms"
        return report
    }
```

```fsharp run
let log = Log.memory ()
timedReport
|> Flow.withClock (Clock.``fixed`` DateTimeOffset.UnixEpoch)
|> Flow.withLogSink log
|> Flow.run ()
|> shouldEqual (Exit.Success "report")
log.Entries |> Seq.map _.Message |> Seq.toList |> shouldEqual ["report built in 0 ms"]
```

A manual clock reports zero elapsed duration only while nobody advances it. `Clock.``fixed`` instant` and
`Clock.fromValue instant` both return `ManualClock`, on .NET and Fable. Sleep does not throw under either: it waits
until the test advances far enough. An unadvanced timed test can therefore remain suspended indefinitely.

## Step time explicitly

`Flow.runTest` installs an epoch manual clock, recording log, seeded random and sequential GUIDs. Use
`Flow.runTestWith` when the test needs those instances. Await facts such as sleeper registration before advancing,
not a wall-clock pause or a guess that the scheduler is idle:

```fsharp run
let platform = TestPlatform.create ()
let registered = Deferred.make<unit, Never, unit> () |> Flow.run () |> Exit.toResult |> Result.defaultWith Never.absurd
let clock = platform.Clock :> IClock
let notifying =
    { new IClock with
        member _.UtcNow() = clock.UtcNow()
        member _.Elapsed() = clock.Elapsed()
        member _.Sleep(delay, token) =
            let pending = clock.Sleep(delay, token)
            Deferred.succeedNow () registered |> ignore
            pending }
let stepped: Flow<unit, Never, int> = flow {
    let! child = Flow.sleep (TimeSpan.FromSeconds 5.) |> Flow.withClock notifying |> Flow.fork
    do! Deferred.await registered
    do! TestClock.advance (TimeSpan.FromSeconds 4.)
    let! waiting = TestClock.sleepers
    do! TestClock.advance (TimeSpan.FromSeconds 1.)
    do! Fiber.join child
    return waiting
}
stepped |> Flow.runTestWith platform () |> shouldEqual (Exit.Success 1)
```

`TestClock.advance` and `sleepers` require the installed clock to be a manual clock; using them with a live clock
produces a defect with a diagnostic message. `TestClock.advanceAll` captures the latest registered deadline when
called and drains only through that horizon. A periodic sleeper cannot extend it; a subsequent sequential sleep
may need another explicit advance. No test runner infers deadlines or advances automatically.

On .NET, due waits are released during `AdvanceBy`, but a continuation can still hop asynchronously. On Fable,
promise continuations always resume on a later microtask. On both platforms, await the released fiber or its next
signal before making another dependent advance. The `TestClock` helpers and `runTest` runners are .NET-only;
Fable tests call `ManualClock.AdvanceBy` directly and then await the fiber.

A missing advance can be diagnosed through a named fiber's registry dump. Name long-lived workers with
`Flow.forkNamed` so the dump identifies the sleeper rather than silently waiting.

`Clock.fromTimeProvider` and `toTimeProvider` bridge .NET `TimeProvider` on supported targets. Process live adapters
and Elmish hosts accept explicit clocks because some of their work begins outside a Flow. See
[scheduling and retries](/scheduling-and-retries/index.html) for the operations that consume runtime time.
