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

## Build a schedule

Choose a schedule based on how many times the flow can run and how long Axial should wait between runs.

### Limit the number of recurrences

Use `Schedule.recurs` to set the number of additional runs.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
// Run up to 6 times: 1 initial run and 5 additional runs.
let fiveMoreTimes = Schedule.recurs 5
```

### Use a fixed delay

Use `Schedule.spaced` to keep running with the same delay between runs.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
// Wait 1 second between runs.
let everySecond = Schedule.spaced (TimeSpan.FromSeconds 1.0)
```

A spaced schedule doesn't stop on its own. The flow continues until it fails, is interrupted, or an outer operation stops it.

### Use exponential backoff

Use `Schedule.exponential` when repeated attempts should wait progressively longer.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
// Wait 100 ms, 200 ms, 400 ms, 800 ms, and so on.
let backoff = Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
```

### Add jitter

If many clients retry at the same time, they can place another burst of load on the service. Jitter spreads those retries across a wider period.

`Schedule.jitteredWith` multiplies each delay by a sampled factor from 0.5 to 1.5. You provide the sample function, which keeps randomness explicit and replaceable in tests.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let policy =
    Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
    |> Schedule.jitteredWith random.NextDouble
```

In application code, get the sample function from the `IRandom` service in `Axial.PlatformService`. In tests, replace it with a function that returns a fixed value.

### Run at a fixed rate

`Schedule.spaced` waits a fixed delay *after* each run, so a run that takes 15 ms on a 50 ms spacing starts every 65 ms.
`Schedule.fixedRate` instead aligns runs to when the first run began: they start at `start + n * period`, however long
each takes.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
// Start a control scan every 50 ms without drifting.
let scanLoop =
    scanOnce
    |> Flow.repeat (Schedule.fixedRate (TimeSpan.FromMilliseconds 50.0))
```

If a run takes longer than a period, the next run starts immediately, once. The schedule then realigns to the next
boundary, so missed ticks are skipped rather than run in a burst.

### Combine schedules

`Schedule.union` continues while either schedule continues and waits for the shorter delay. Combining exponential
backoff with a spaced schedule caps the backoff:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
// Exponential back-off capped at 30 s, retrying forever
let reconnect =
    Schedule.exponential (TimeSpan.FromMilliseconds 200.0)
    |> Schedule.union (Schedule.spaced (TimeSpan.FromSeconds 30.0))
```

`Schedule.intersect` continues only while both schedules continue and waits for the longer delay. Combining a
recurrence limit with backoff bounds the number of attempts:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
// At most 10 retries, with exponential back-off
let limitedBackoff =
    Schedule.recurs 10
    |> Schedule.intersect (Schedule.exponential (TimeSpan.FromMilliseconds 200.0))
```

Both pair the two schedules' outputs, with the piped-in schedule first. `intersect` emits `'output * 'otherOutput`.
`union` emits `'output option * 'otherOutput option`, because a side that has stopped has no output; it contributes
`None` while the other side continues.

`Schedule.andThen` runs one schedule until it stops, then hands over to another. The second schedule counts from zero
when it takes over, so its delays start from the beginning:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
// Three quick retries, then up to five slower ones with backoff
let patient =
    Schedule.spaced (TimeSpan.FromMilliseconds 100.0)
    |> Schedule.recursAtMost 3
    |> Schedule.andThen (Schedule.exponential (TimeSpan.FromSeconds 1.0) |> Schedule.recursAtMost 5)
```

Its output is `Choice1Of2` while the first schedule decides and `Choice2Of2` after the hand-over.

### Stop on time or on the schedule's own output

`Schedule.upTo` gives a schedule a total time budget, measured from when the first run began. The schedule continues
while less than the budget has passed; it never cuts a run short. To cap the number of retries instead, use
`Schedule.recursAtMost`:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
// Retry with backoff, but give up after two minutes in total
let bounded =
    Schedule.exponential (TimeSpan.FromMilliseconds 200.0)
    |> Schedule.upTo (TimeSpan.FromMinutes 2.0)
```

`Schedule.whileOutput` and `Schedule.untilOutput` stop on what the schedule itself produces, where `whileInput` looks
at the error or value being retried. For example, stop once exponential backoff reaches 30 seconds:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let untilSlow =
    Schedule.exponential (TimeSpan.FromMilliseconds 200.0)
    |> Schedule.whileOutput (fun delay -> delay < TimeSpan.FromSeconds 30.0)
```

`Schedule.elapsed` recurs without waiting and emits the time since the first run, and `Schedule.map` transforms any
schedule's output.

### Restart the count after a healthy run

`Schedule.resetAfter` treats a failure that follows a long, successful run as a new problem. When a run lasted at
least the given time, the wrapped schedule starts counting from zero again, so `recurs` and `exponential` restart too:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
// Five restarts for a crash loop, but a crash after ten healthy minutes gets the full budget back
worker |> Flow.supervise (Schedule.recurs 5 |> Schedule.resetAfter (TimeSpan.FromMinutes 10.0))
```

## Retry failed flows

Use `Flow.retry` to rerun a flow after an expected domain failure (`Cause.Fail`).

`Flow.retry` doesn't retry defects (`Cause.Die`) or interruptions (`Cause.Interrupt`). Axial passes them through without consulting the schedule.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let unstableCall =
    flow {
        return! Flow.fail "temporary-error"
    }

// Try up to 4 times: 1 initial attempt and 3 retries.
let resilientCall =
    unstableCall
    |> Flow.retry (Schedule.recurs 3)
```

The retry stops when the flow succeeds or the schedule declines another run. If the schedule stops after a failure, the flow returns that failure.

Each attempt runs in its own child scope. When an attempt fails and another is scheduled, the failed attempt's
finalizers run before the retry starts. The successful attempt's scope stays open until the enclosing scope closes, so a
connection or handle it returns is still usable.

### Retry only some errors

The schedule sees each typed error as its input. `Schedule.whileInput` continues only for errors that satisfy a
predicate, and `Schedule.recursAtMost` caps the number of retries of any schedule:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let isTransient error =
    match error with
    | Unavailable
    | RateLimited -> true
    | NotFound -> false

let resilientFetch =
    fetch
    |> Flow.retry (
        Schedule.exponential (TimeSpan.FromMilliseconds 200.0)
        |> Schedule.recursAtMost 3
        |> Schedule.whileInput isTransient)
```

`Schedule.untilInput` is the negation: it stops on the first input that satisfies the predicate.

### Describe a retry with a record

A `Retry` record names the three choices most retries make, and `Retry.schedule` turns it into a schedule. Start
from `Retry.defaults` (3 retries, exponential backoff from 100 ms capped at 10 s, every error retried) and change
the fields you need:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let resilientFetch =
    fetch
    |> Flow.retry (Retry.schedule { Retry.defaults with Retries = 5; When = isTransient })
```

`Retries` counts retries after the first attempt, the same as `Schedule.recurs`. `Backoff` is `Backoff.NoDelay`,
`Backoff.Fixed delay`, or `Backoff.Exponential(initial, max)`. Write a `Schedule` pipeline when you need jitter,
fixed-rate timing, or elapsed-time limits.

## Repeat successful flows

Use `Flow.repeat` to run a successful flow again. This is useful for polling, heartbeats, and recurring background work.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let pollStatus =
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
> (Schedule.fixedRate (System.TimeSpan.FromMilliseconds 20.0)
-  |> FlowStream.fromSchedule
-  |> FlowStream.take 3
-  |> FlowStream.runCollect
-  : Flow<unit, Never, int list>)
- |> Flow.run ();;
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
