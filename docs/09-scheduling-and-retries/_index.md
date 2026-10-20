---
title: Scheduling and Retries
description: Retry failed workflows and repeat successful workflows with reusable schedules.
---

# Scheduling and Retries

A `Schedule` decides whether a flow should run again and how long to wait. Creating a schedule doesn't run anything. Apply it with `Flow.retry` or `Flow.repeat`.

Schedules don't store attempt state. `Flow.retry` and `Flow.repeat` track attempts for each run, so you can reuse one schedule value across unrelated flows.

`Schedule` works on .NET and Fable's JavaScript target.

## How schedules work

A schedule makes two decisions after each flow execution:

1. Whether to run the flow again.
2. How long to wait before the next run.

The source flow always runs once before Axial consults the schedule. For example, `Schedule.recurs 3` allows three more runs, for up to four runs in total.

`runsUntilStopped` counts attempts by a flow that always fails. `firstOutputs` reads a schedule's first outputs as a stream. The checks use millisecond delays because the waits happen during the test:

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
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
let runsUntilStopped (schedule: Schedule<ClockEnvironment, string, 'output>) : int =
    let runs = ref 0

    (Flow.delay (fun () ->
        runs.Value <- runs.Value + 1
        Flow.fail "always")
     : Flow<ClockEnvironment, string, unit>)
    |> Flow.retry schedule
    |> Flow.run (ClockEnvironment Clock.live)
    |> ignore

    runs.Value

let firstOutputs (count: int) (schedule: Schedule<ClockEnvironment, unit, 'output>) : 'output list =
    match schedule |> FlowStream.fromSchedule |> FlowStream.take count |> FlowStream.runCollect |> Flow.run (ClockEnvironment Clock.live) with
    | Exit.Success outputs -> outputs
    | Exit.Failure _ -> []

let ms (value: float) = TimeSpan.FromMilliseconds value
```

## Build a schedule

Choose a schedule based on how many times the flow can run and how long Axial should wait between runs.

### Limit the number of recurrences

Use `Schedule.recurs` to set the number of additional runs.

```fsharp
// Run up to 6 times: 1 initial run and 5 additional runs.
let fiveMoreTimes = Schedule.recurs 5
```

```fsharp run
runsUntilStopped fiveMoreTimes |> shouldEqual 6
```

### Use a fixed delay

Use `Schedule.spaced` to keep running with the same delay between runs.

```fsharp
// Wait 1 second between runs.
let everySecond : Schedule<ClockEnvironment, string, _> = Schedule.spaced (TimeSpan.FromSeconds 1.0)
```

The output counts the runs so far:

```fsharp run
firstOutputs 3 (Schedule.spaced (ms 1.0)) |> shouldEqual [ 0; 1; 2 ]
```

`Schedule.spaced` continues until the flow fails, is interrupted, or an outer operation stops it.

### Use exponential backoff

Use `Schedule.exponential` when repeated attempts should wait progressively longer.

```fsharp
// Wait 100 ms, 200 ms, 400 ms, 800 ms, and so on.
let backoff : Schedule<ClockEnvironment, string, _> = Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
```

Its output is the delay it chose:

```fsharp run
firstOutputs 4 (Schedule.exponential (ms 1.0)) |> shouldEqual [ ms 1.0; ms 2.0; ms 4.0; ms 8.0 ]
```

### Add jitter

If many clients retry at the same time, they can place another burst of load on the service. Jitter spreads those retries across a wider period.

`Schedule.jitteredWith` multiplies each delay by a sampled factor from 0.5 to 1.5. You provide the sample function — a
`unit -> float` — which keeps randomness explicit and replaceable in tests.

That function is `IRandom.NextDouble` from `Axial.PlatformService`, so the schedule never constructs `System.Random()`
itself. Take the `IRandom` from your environment and pass its `NextDouble` method:

```fsharp
let policy (random: IRandom) : Schedule<ClockEnvironment, string, _> =
    Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
    |> Schedule.jitteredWith random.NextDouble
```

In application code, the `IRandom` comes from the [base runtime](/services/platform-services/random-and-guid.html) or
a field of your own environment record. In tests, pass a deterministic `IRandom` such as
`Random.fromFixed 0 0.25 0uy`, whose `NextDouble` always returns `0.25`, so every delay is multiplied by `0.75`:

```fsharp
let testPolicy : Schedule<ClockEnvironment, string, _> =
    policy (Random.fromFixed 0 0.25 0uy)
```

To build the jittered schedule from the environment rather than an ambient random source, read `IRandom` inside the
workflow and apply its `NextDouble` as the `unit -> float` sample:

```fsharp
let jitteredFetch (fetch: Flow<BaseRuntime, string, string>) : Flow<BaseRuntime, string, string> =
    flow {
        let! random = Random.service
        let schedule =
            Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
            |> Schedule.jitteredWith random.NextDouble
        return! fetch |> Flow.retry schedule
    }
```

```fsharp run
let attempts = ref 0

let transientFetch : Flow<BaseRuntime, string, string> =
    flow {
        attempts.Value <- attempts.Value + 1
        if attempts.Value < 2 then return! Flow.fail "transient" else return "payload"
    }

let runtime = { BaseRuntime.liveValue with Random = Random.fromFixed 0 0.25 0uy }

jitteredFetch transientFetch |> Flow.run runtime |> shouldEqual (Exit.Success "payload")
```

`Random.service` is the type-directed accessor for the `IHasRandom` contract; the fixed random makes the retried
attempt succeed deterministically.

### Run at a fixed rate

`Schedule.spaced` waits a fixed delay *after* each run, so a run that takes 15 ms on a 50 ms spacing starts every 65 ms.
`Schedule.fixedRate` instead aligns runs to when the first run began: they start at `start + n * period`, however long
each takes.

```fsharp
// Start a control scan every 50 ms without drifting.
let scanOnce : Flow<ClockEnvironment, Never, unit> = Flow.ok ()

let scanLoop =
    scanOnce
    |> Flow.repeat (Schedule.fixedRate (TimeSpan.FromMilliseconds 50.0))
```

If a run takes longer than a period, the next run starts immediately, once. The schedule then realigns to the next
boundary, so missed ticks are skipped rather than run in a burst.

### Combine and shape schedules

`union`, `intersect`, and `andThen` compose schedules; `upTo` sets a time budget; `whileOutput`/`untilOutput`,
`elapsed`, and `map` stop or transform on the schedule's own output; and `resetAfter` restarts the count after a long
run. These advanced combinators are covered in [Combining schedules](combining-schedules.html).

## Retry failed flows

Use `Flow.retry` to rerun a flow after an expected domain failure (`Cause.Fail`).

`Flow.retry` doesn't retry defects (`Cause.Die`) or interruptions (`Cause.Interrupt`). Axial passes them through without consulting the schedule.

```fsharp
let callsMade = ref 0

let unstableCall : Flow<ClockEnvironment, string, unit> =
    flow {
        callsMade.Value <- callsMade.Value + 1
        return! Flow.fail "temporary-error"
    }

// Try up to 4 times: 1 initial attempt and 3 retries.
let resilientCall =
    unstableCall
    |> Flow.retry (Schedule.recurs 3)
```

```fsharp run
resilientCall |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Failure(Cause.Fail "temporary-error"))
callsMade.Value |> shouldEqual 4
```

The retry stops when the flow succeeds or the schedule declines another run. If the schedule stops after a failure, the flow returns that failure.

Each attempt runs in its own child scope. When an attempt fails and another is scheduled, the failed attempt's
finalizers run before the retry starts. The successful attempt's scope stays open until the enclosing scope closes, so a
connection or handle it returns is still usable.

### Retry only some errors

The schedule sees each typed error as its input. `Schedule.whileInput` continues only for errors that satisfy a
predicate, and `Schedule.recursAtMost` caps the number of retries of any schedule:

```fsharp
type FetchError =
    | Unavailable
    | RateLimited
    | NotFound

/// A fetch that fails with each error in turn, then succeeds.
let fetchFailing (errors: FetchError list) : Flow<ClockEnvironment, FetchError, string> =
    let remaining = ref errors

    Flow.delay (fun () ->
        match remaining.Value with
        | error :: rest ->
            remaining.Value <- rest
            Flow.fail error
        | [] -> Flow.ok "payload")

let isTransient error =
    match error with
    | Unavailable
    | RateLimited -> true
    | NotFound -> false

let resilientFetch (fetch: Flow<ClockEnvironment, FetchError, string>) =
    fetch
    |> Flow.retry (
        Schedule.exponential (TimeSpan.FromMilliseconds 200.0)
        |> Schedule.recursAtMost 3
        |> Schedule.whileInput isTransient)
```

```fsharp run
fetchFailing [ Unavailable; RateLimited ] |> resilientFetch |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success "payload")
fetchFailing [ Unavailable; NotFound ] |> resilientFetch |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Failure(Cause.Fail NotFound))
```

`Schedule.untilInput` is the negation: it stops on the first input that satisfies the predicate.

### Describe a retry with a record

A `Retry` record names the three choices most retries make, and `Retry.schedule` turns it into a schedule. Start
from `Retry.defaults` (3 retries, exponential backoff from 100 ms capped at 10 s, every error retried) and change
the fields you need:

```fsharp
let resilientFetchWithRecord (fetch: Flow<ClockEnvironment, FetchError, string>) =
    fetch
    |> Flow.retry (Retry.schedule { Retry.defaults with Retries = 5; When = isTransient; Backoff = Backoff.NoDelay })
```

```fsharp run
fetchFailing [ RateLimited; RateLimited; Unavailable ] |> resilientFetchWithRecord |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success "payload")
```

`Retries` counts retries after the first attempt, the same as `Schedule.recurs`. `Backoff` is `Backoff.NoDelay`,
`Backoff.Fixed delay`, or `Backoff.Exponential(initial, max)`. Write a `Schedule` pipeline when you need jitter,
fixed-rate timing, or elapsed-time limits.

## Repeat successful flows

Use `Flow.repeat` to run a successful flow again. The schedule's input is the successful value.

```fsharp
let pollStatus : Flow<ClockEnvironment, Never, string> =
    flow {
        return "Still working"
    }

// pollStatus has error type Never, so it cannot fail with a typed error. With a schedule that never stops,
// this repeats forever; the caller must interrupt it or let an outer scope end it.
let recurringPoll =
    pollStatus
    |> Flow.repeat (Schedule.spaced (TimeSpan.FromSeconds 5.0))

// A bounded variant: the initial run plus up to three repeats.
let pollFourTimes =
    pollStatus
    |> Flow.repeat (Schedule.spaced (TimeSpan.FromSeconds 5.0) |> Schedule.recursAtMost 3)
```

`Flow.repeat` consults the schedule only after a successful run. A typed failure, defect, or interruption stops the
repetition immediately; a schedule that never stops keeps repeating a successful flow until one of those happens or the
caller interrupts it.

The schedule receives each successful value as its input, so `whileInput` can stop the loop on the value itself:

```fsharp
let repeatCount = ref 0

let ticking : Flow<ClockEnvironment, Never, int> =
    flow {
        repeatCount.Value <- repeatCount.Value + 1
        return repeatCount.Value
    }

let repeatUntilThree =
    ticking
    |> Flow.repeat (Schedule.spaced TimeSpan.Zero |> Schedule.whileInput (fun value -> value < 3))
```

```fsharp run
repeatCount.Value <- 0
repeatUntilThree |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success 3)
repeatCount.Value |> shouldEqual 3
```

A failure ends the loop immediately, without consulting the schedule again:

```fsharp
let failingRepeatCount = ref 0

let failingTick : Flow<ClockEnvironment, string, int> =
    flow {
        failingRepeatCount.Value <- failingRepeatCount.Value + 1
        if failingRepeatCount.Value = 2 then return! Flow.fail "stopped" else return failingRepeatCount.Value
    }

let failingRepeat = failingTick |> Flow.repeat (Schedule.spaced TimeSpan.Zero)
```

```fsharp run
failingRepeatCount.Value <- 0
failingRepeat |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Failure(Cause.Fail "stopped"))
failingRepeatCount.Value |> shouldEqual 2
```

## Drive a stream from a schedule

`FlowStream.fromSchedule` turns a schedule into a stream of its outputs, each emitted after the delay the schedule
chooses. It ends when the schedule stops. With `Schedule.fixedRate` it is a tick stream that keeps to its grid even
when the consumer is slow: ticks the consumer was too busy to take are skipped, not delivered in a burst.

```fsharp transcript
> open Axial.PlatformService;;
> (Schedule.fixedRate (System.TimeSpan.FromMilliseconds 20.0)
-  |> FlowStream.fromSchedule
-  |> FlowStream.take 3
-  |> FlowStream.runCollect
-  : Flow<ClockEnvironment, Never, int list>)
- |> Flow.run (ClockEnvironment Clock.live);;
val it: Exit<int list,Never> = Success [0; 1; 2]
```

## Schedule API reference

`Schedule`'s full signature list — `recurs`, `spaced`, `exponential`, `jitteredWith`, `fixedRate`, `union`,
`intersect`, `andThen`, `whileInput`/`untilInput`, `whileOutput`/`untilOutput`, `recursAtMost`, `upTo`,
`resetAfter`, `elapsed`, `map`, plus `Flow.retry`, `Flow.repeat`, and `FlowStream.fromSchedule` — is in the
[Schedule API reference](/api/Axial.ScheduleModule.html).
