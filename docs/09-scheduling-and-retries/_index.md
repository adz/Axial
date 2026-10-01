---
title: Scheduling and Retries
description: Retry failed workflows and repeat successful workflows with reusable schedules.
---

# Scheduling and Retries

A `Schedule` decides whether a flow should run again and how long to wait. Creating a schedule doesn't run anything. Apply it with `Flow.retry` or `Flow.repeat`.

Use schedules for tasks such as retrying a request, adding exponential backoff, polling a service, or running a heartbeat.

Schedules don't store attempt state. `Flow.retry` and `Flow.repeat` track attempts for each run, so you can reuse one schedule value across unrelated flows.

`Schedule` works on .NET and Fable's JavaScript target.

## How schedules work

A schedule makes two decisions after each flow execution:

1. Whether to run the flow again.
2. How long to wait before the next run.

The source flow always runs once before Axial consults the schedule. For example, `Schedule.recurs 3` allows three more runs, for up to four runs in total.

The examples on this page check each schedule with two helpers. `runsUntilStopped` counts how many times a flow that
always fails runs under a schedule. `firstOutputs` reads a schedule's first outputs as a stream. Their delays really
happen, so the checks use millisecond versions of the schedules they describe:

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

`spaced` never stops on its own, and its output counts the runs so far:

```fsharp run
firstOutputs 3 (Schedule.spaced (ms 1.0)) |> shouldEqual [ 0; 1; 2 ]
```

A spaced schedule doesn't stop on its own. The flow continues until it fails, is interrupted, or an outer operation stops it.

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

`Schedule.jitteredWith` multiplies each delay by a sampled factor from 0.5 to 1.5. You provide the sample function, which keeps randomness explicit and replaceable in tests.

```fsharp
let policy : Schedule<ClockEnvironment, string, _> =
    Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
    |> Schedule.jitteredWith (System.Random().NextDouble)
```

In application code, get the sample function from the `IRandom` service in `Axial.PlatformService`. In tests, replace it with a function that returns a fixed value.

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

### Combine schedules

`Schedule.union` continues while either schedule continues and waits for the shorter delay. Combining exponential
backoff with a spaced schedule caps the backoff:

```fsharp
// Exponential back-off capped at 30 s, retrying forever
let reconnect : Schedule<ClockEnvironment, string, _> =
    Schedule.exponential (TimeSpan.FromMilliseconds 200.0)
    |> Schedule.union (Schedule.spaced (TimeSpan.FromSeconds 30.0))
```

`union` continues while either side does and outputs both sides' outputs; a stopped side would show `None`:

```fsharp run
firstOutputs 3 (Schedule.exponential (ms 1.0) |> Schedule.union (Schedule.spaced (ms 2.0)))
|> shouldEqual [ Some(ms 1.0), Some 0; Some(ms 2.0), Some 1; Some(ms 4.0), Some 2 ]
```

`Schedule.intersect` continues only while both schedules continue and waits for the longer delay. Combining a
recurrence limit with backoff bounds the number of attempts:

```fsharp
// At most 10 retries, with exponential back-off
let limitedBackoff : Schedule<ClockEnvironment, string, _> =
    Schedule.recurs 10
    |> Schedule.intersect (Schedule.exponential (TimeSpan.FromMilliseconds 200.0))
```

```fsharp run
runsUntilStopped (Schedule.recurs 10 |> Schedule.intersect (Schedule.exponential (TimeSpan.FromTicks 10L))) |> shouldEqual 11
```

Both pair the two schedules' outputs, with the piped-in schedule first. `intersect` emits `'output * 'otherOutput`.
`union` emits `'output option * 'otherOutput option`, because a side that has stopped has no output; it contributes
`None` while the other side continues.

`Schedule.andThen` runs one schedule until it stops, then hands over to another. The second schedule counts from zero
when it takes over, so its delays start from the beginning:

```fsharp
// Three quick retries, then up to five slower ones with backoff
let patient : Schedule<ClockEnvironment, string, _> =
    Schedule.spaced (TimeSpan.FromMilliseconds 100.0)
    |> Schedule.recursAtMost 3
    |> Schedule.andThen (Schedule.exponential (TimeSpan.FromSeconds 1.0) |> Schedule.recursAtMost 5)
```

```fsharp run
Schedule.spaced (ms 0.1)
|> Schedule.recursAtMost 3
|> Schedule.andThen (Schedule.exponential (ms 0.1) |> Schedule.recursAtMost 5)
|> runsUntilStopped
|> shouldEqual 9
```

One initial run, three quick retries, then five slower ones.

Its output is `Choice1Of2` while the first schedule decides and `Choice2Of2` after the hand-over.

### Stop on time or on the schedule's own output

`Schedule.upTo` gives a schedule a total time budget, measured from when the first run began. The schedule continues
while less than the budget has passed; it never cuts a run short. To cap the number of retries instead, use
`Schedule.recursAtMost`:

```fsharp
// Retry with backoff, but give up after two minutes in total
let bounded : Schedule<ClockEnvironment, string, _> =
    Schedule.exponential (TimeSpan.FromMilliseconds 200.0)
    |> Schedule.upTo (TimeSpan.FromMinutes 2.0)
```

With a 20 ms budget, delays of 1, 2, 4, 8, and 16 ms allow at most six runs:

```fsharp run
let budgeted = runsUntilStopped (Schedule.exponential (ms 1.0) |> Schedule.upTo (ms 20.0))
(budgeted >= 2 && budgeted <= 6) |> shouldEqual true
```

`Schedule.whileOutput` and `Schedule.untilOutput` stop on what the schedule itself produces, where `whileInput` looks
at the error or value being retried. For example, stop once exponential backoff reaches 30 seconds:

```fsharp
let untilSlow : Schedule<ClockEnvironment, string, _> =
    Schedule.exponential (TimeSpan.FromMilliseconds 200.0)
    |> Schedule.whileOutput (fun delay -> delay < TimeSpan.FromSeconds 30.0)
```

```fsharp run
runsUntilStopped (Schedule.exponential (ms 1.0) |> Schedule.whileOutput (fun delay -> delay < ms 8.0)) |> shouldEqual 4
```

`Schedule.elapsed` recurs without waiting and emits the time since the first run, and `Schedule.map` transforms any
schedule's output.

### Restart the count after a healthy run

`Schedule.resetAfter` treats a failure that follows a long, successful run as a new problem. When a run lasted at
least the given time, the wrapped schedule starts counting from zero again, so `recurs` and `exponential` restart too:

```fsharp
// Five restarts for a crash loop, but a crash after ten healthy minutes gets the full budget back
let worker : Flow<ClockEnvironment, Never, unit> = Flow.ok ()

let supervisedWorker =
    worker |> Flow.supervise (Schedule.recurs 5 |> Schedule.resetAfter (TimeSpan.FromMinutes 10.0))
```

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

`NotFound` is not transient, so the retry stops there even though retries remain.

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

Use `Flow.repeat` to run a successful flow again. This is useful for polling, heartbeats, and recurring background work.

```fsharp
let pollStatus : Flow<ClockEnvironment, Never, string> =
    flow {
        return "Still working"
    }

// Poll every 5 seconds until the flow fails or is interrupted.
let recurringPoll =
    pollStatus
    |> Flow.repeat (Schedule.spaced (TimeSpan.FromSeconds 5.0))
```

`Flow.repeat` consults the schedule only after a successful run. A typed failure, defect, or interruption stops the repetition immediately.

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

| Function | Signature | Behavior |
| :--- | :--- | :--- |
| `recurs` | `int -> Schedule<'env, 'input, int>` | Allows exactly `n` additional runs and emits the zero-based recurrence index. |
| `spaced` | `TimeSpan -> Schedule<'env, 'input, int>` | Continues with a fixed delay and emits the zero-based recurrence index. |
| `exponential` | `TimeSpan -> Schedule<'env, 'input, TimeSpan>` | Continues with a delay that doubles after each run. |
| `jitteredWith` | `(unit -> float) -> Schedule<'env, 'input, 'output> -> Schedule<'env, 'input, 'output>` | Adjusts each delay by a sampled factor, normally from 0.5 to 1.5. |
| `fixedRate` | `TimeSpan -> Schedule<'env, 'input, int>` | Starts runs at `start + n * period` and emits the zero-based recurrence index. |
| `union` | `Schedule<'env, 'input, 'o2> -> Schedule<'env, 'input, 'o1> -> Schedule<'env, 'input, 'o1 option * 'o2 option>` | Continues while either continues, with the shorter delay. |
| `intersect` | `Schedule<'env, 'input, 'o2> -> Schedule<'env, 'input, 'o1> -> Schedule<'env, 'input, 'o1 * 'o2>` | Continues while both continue, with the longer delay. |
| `andThen` | `Schedule<'env, 'input, 'o2> -> Schedule<'env, 'input, 'o1> -> Schedule<'env, 'input, Choice<'o1, 'o2>>` | Runs the first schedule until it stops, then the second from its start. |
| `whileInput` / `untilInput` | `('input -> bool) -> Schedule<'env, 'input, 'output> -> Schedule<'env, 'input, 'output>` | Continues while (until) the retried error or repeated value satisfies the predicate. |
| `whileOutput` / `untilOutput` | `('output -> bool) -> Schedule<'env, 'input, 'output> -> Schedule<'env, 'input, 'output>` | Continues while (until) the schedule's own output satisfies the predicate. |
| `recursAtMost` | `int -> Schedule<'env, 'input, 'output> -> Schedule<'env, 'input, 'output>` | Stops after at most `n` recurrences, keeping the schedule's delays. |
| `upTo` | `TimeSpan -> Schedule<'env, 'input, 'output> -> Schedule<'env, 'input, 'output>` | Stops once a total time budget has passed since the first run. |
| `resetAfter` | `TimeSpan -> Schedule<'env, 'input, 'output> -> Schedule<'env, 'input, 'output>` | Restarts the count after a run that lasted at least the given time. |
| `elapsed` | `Schedule<'env, 'input, TimeSpan>` | Recurs without waiting and emits the time since the first run. |
| `map` | `('o1 -> 'o2) -> Schedule<'env, 'input, 'o1> -> Schedule<'env, 'input, 'o2>` | Transforms the schedule's output. |
| `Flow.retry` | `Schedule<'env, 'error, 'output> -> Flow<'env, 'error, 'value> -> Flow<'env, 'error, 'value>` | Retries the flow after `Cause.Fail`. |
| `Flow.repeat` | `Schedule<'env, 'value, 'output> -> Flow<'env, 'error, 'value> -> Flow<'env, 'error, 'value>` | Repeats the flow after success. |
| `FlowStream.fromSchedule` | `Schedule<'env, unit, 'output> -> FlowStream<'env, 'error, 'output>` | Emits each output after its delay; ends when the schedule stops. |
