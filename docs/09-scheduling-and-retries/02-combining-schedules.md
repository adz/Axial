---
title: Combining schedules
linkTitle: Combining schedules
description: Compose schedules with union, intersect, andThen; stop on time or output; restart the count after a long run.
---

# Combining schedules

The [scheduling guide](index.html) covers the basic schedule builders, retry, and repeat. This page covers the
combinators that shape several schedules into one: capping a backoff, handing from one schedule to another, stopping
on a budget or on the schedule's own output, and restarting the count after a healthy run.

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

## Combine schedules

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

Its output is `Choice1Of2` while the first schedule decides and `Choice2Of2` after the hand-over.

## Stop on time or on the schedule's own output

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

## Restart the count after a long run

`Schedule.resetAfter` restarts the wrapped schedule's count when the run that just finished lasted at least the given
time. The threshold measures that run's own duration, not a previous successful execution: with `Flow.supervise` it is
how long the worker ran before it crashed. When the threshold is met, the wrapped schedule sees attempt 0 again, so
`recurs` and `exponential` restart too:

```fsharp
// Five restarts for a crash loop, but a crash after ten minutes of good work gets the full budget back.
let worker : Flow<ClockEnvironment, Never, unit> =
    flow {
        do! Flow.sleep (TimeSpan.FromMinutes 10.0)
        return! Flow.die (InvalidOperationException "crash")
    }

let supervisedWorker =
    worker |> Flow.supervise (Schedule.recurs 5 |> Schedule.resetAfter (TimeSpan.FromMinutes 10.0))
```

The full signature list for every combinator is in the
[Schedule API reference](/api/Axial.ScheduleModule.html).
