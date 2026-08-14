---
title: Scheduling and Retries
description: Declarative policies for retrying failures and repeating successes in Axial.
---

# Scheduling and Retries

A `Schedule` is a value, not an action: it describes when a workflow *should* run again and how long to wait, but
building one does nothing by itself. It only takes effect once you hand it to `Schedule.retry` (rerun on failure) or
`Schedule.repeat` (rerun on success) — those two are what actually run a flow against the schedule. Use schedules for
retries, exponential backoff with jitter, and recurring tasks.

> **Note:** `Schedule` is currently available on **.NET** only.

## Basic Schedules

A schedule decides two things:
1. Whether to continue (recur).
2. How long to wait before the next attempt.

### Fixed Number of Recursions
```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
// Recur 5 times (6 attempts total)
let fiveTimes = Schedule.recurs 5
```

### Fixed Spacing
```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
// Recur indefinitely with 1 second between attempts
let everySecond = Schedule.spaced (TimeSpan.FromSeconds 1.0)
```

### Exponential Backoff
```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
// Delays: 100ms, 200ms, 400ms, 800ms...
let backoff = Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
```

### Adding Jitter
Jitter adds randomness to delays to prevent "thundering herd" problems in distributed systems. `Schedule.jitteredWith` multiplies the current delay by a factor between 0.5x and 1.5x, sampled from a source you supply, so the randomness stays an explicit, testable dependency instead of an ambient one.
```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let policy =
    Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
    |> Schedule.jitteredWith random.NextDouble
```

In application code, prefer taking the sample function from `Axial.PlatformService`'s `IRandom` service (`Random.service`) rather than constructing a `System.Random` inline, so jitter can be replaced with a deterministic source in tests.

## Retrying Failures

Use `Schedule.retry` to apply a schedule to a flow that might fail with an expected domain error (`Cause.Fail`).

**Important:** `Schedule.retry` retries only `Cause.Fail`. `Cause.Die` and `Cause.Interrupt` pass through unchanged, so defects and cancellation are not translated into retries.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let unstableCall = 
    flow {
        return! Flow.fail "temporary-error"
    }

// This will attempt the call up to 4 times (initial + 3 retries)
let resilientCall = 
    unstableCall |> Schedule.retry (Schedule.recurs 3)
```

## Repeating Successes

Use `Schedule.repeat` to execute a successful flow again. This is useful for polling, heartbeats, or recurring background tasks.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let pollStatus = 
    flow {
        return "Still working"
    }

// This will poll every 5 seconds until it fails or is cancelled
let recurringPoll = 
    pollStatus |> Schedule.repeat (Schedule.spaced (TimeSpan.FromSeconds 5.0))
```

## API Reference: Module `Schedule`

| Function | Signature | Description |
| :--- | :--- | :--- |
| `recurs` | `int -> Schedule<'env, 'i, int>` | Recurs exactly `n` times. The output value is the attempt index. |
| `spaced` | `TimeSpan -> Schedule<'env, 'i, int>` | Recurs indefinitely with a fixed delay. |
| `exponential` | `TimeSpan -> Schedule<'env, 'i, TimeSpan>` | Recurs indefinitely with doubling delays. |
| `jitteredWith` | `(unit -> float) -> Schedule<'env, 'i, 'o> -> Schedule<'env, 'i, 'o>` | Wraps a schedule to add jitter (0.5x to 1.5x), sampled from the supplied source. |
| `retry` | `Schedule<'env, 'error, 'output> -> Flow<'env, 'error, 'value> -> Flow<'env, 'error, 'value>` | Retries the flow on `Cause.Fail` only. |
| `repeat` | `Schedule<'env, 'value, 'output> -> Flow<'env, 'error, 'value> -> Flow<'env, 'error, 'value>` | Repeats the flow on success. |
