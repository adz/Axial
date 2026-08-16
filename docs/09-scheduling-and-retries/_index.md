---
title: Scheduling and Retries
description: Retry failed workflows and repeat successful workflows with reusable schedules.
---

# Scheduling and Retries

A `Schedule` decides whether a flow should run again and how long to wait. Creating a schedule doesn't run anything. Apply it with `Schedule.retry` or `Schedule.repeat`.

Use schedules for tasks such as retrying a request, adding exponential backoff, polling a service, or running a heartbeat.

Schedules don't store attempt state. `Schedule.retry` and `Schedule.repeat` track attempts for each run, so you can reuse one schedule value across unrelated flows.

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

## Retry failed flows

Use `Schedule.retry` to rerun a flow after an expected domain failure (`Cause.Fail`).

`Schedule.retry` doesn't retry defects (`Cause.Die`) or interruptions (`Cause.Interrupt`). Axial passes them through without consulting the schedule.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let unstableCall =
    flow {
        return! Flow.fail "temporary-error"
    }

// Try up to 4 times: 1 initial attempt and 3 retries.
let resilientCall =
    unstableCall
    |> Schedule.retry (Schedule.recurs 3)
```

The retry stops when the flow succeeds or the schedule declines another run. If the schedule stops after a failure, the flow returns that failure.

## Repeat successful flows

Use `Schedule.repeat` to run a successful flow again. This is useful for polling, heartbeats, and recurring background work.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let pollStatus =
    flow {
        return "Still working"
    }

// Poll every 5 seconds until the flow fails or is interrupted.
let recurringPoll =
    pollStatus
    |> Schedule.repeat (Schedule.spaced (TimeSpan.FromSeconds 5.0))
```

`Schedule.repeat` consults the schedule only after a successful run. A typed failure, defect, or interruption stops the repetition immediately.

## Schedule API reference

| Function | Signature | Behavior |
| :--- | :--- | :--- |
| `recurs` | `int -> Schedule<'env, 'input, int>` | Allows exactly `n` additional runs and emits the zero-based recurrence index. |
| `spaced` | `TimeSpan -> Schedule<'env, 'input, int>` | Continues with a fixed delay and emits the zero-based recurrence index. |
| `exponential` | `TimeSpan -> Schedule<'env, 'input, TimeSpan>` | Continues with a delay that doubles after each run. |
| `jitteredWith` | `(unit -> float) -> Schedule<'env, 'input, 'output> -> Schedule<'env, 'input, 'output>` | Adjusts each delay by a sampled factor, normally from 0.5 to 1.5. |
| `retry` | `Schedule<'env, 'error, 'output> -> Flow<'env, 'error, 'value> -> Flow<'env, 'error, 'value>` | Retries the flow after `Cause.Fail`. |
| `repeat` | `Schedule<'env, 'value, 'output> -> Flow<'env, 'error, 'value> -> Flow<'env, 'error, 'value>` | Repeats the flow after success. |
