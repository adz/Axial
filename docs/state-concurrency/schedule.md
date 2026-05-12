---
weight: 20
title: Schedule (Retry & Repeat)
description: Declarative policies for retrying failures and repeating successes in FsFlow.
---

# Schedule (Retry & Repeat)

The `Schedule` module provides a powerful DSL for describing policies that govern how and when a workflow should be executed again. You can use schedules to implement sophisticated retry strategies (like exponential backoff with jitter) or recurring tasks.

> **Note:** `Schedule` is currently available on **.NET** only.

## Basic Schedules

A schedule decides two things:
1. Whether to continue (recur).
2. How long to wait before the next attempt.

### Fixed Number of Recursions
```fsharp
// Recur 5 times (6 attempts total)
let fiveTimes = Schedule.recurs 5
```

### Fixed Spacing
```fsharp
// Recur indefinitely with 1 second between attempts
let everySecond = Schedule.spaced (TimeSpan.FromSeconds 1.0)
```

### Exponential Backoff
```fsharp
// Delays: 100ms, 200ms, 400ms, 800ms...
let backoff = Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
```

### Adding Jitter
Jitter adds randomness to delays to prevent "thundering herd" problems in distributed systems. `Schedule.jittered` adds a random factor between 0.5x and 1.5x to the current delay.
```fsharp
let policy = 
    Schedule.exponential (TimeSpan.FromMilliseconds 100.0)
    |> Schedule.jittered
```

## Retrying Failures

Use `Flow.Retry` to apply a schedule to a flow that might fail with an expected domain error (`Cause.Fail`). 

**Important:** If the flow fails with a defect (`Cause.Die`) or is interrupted (`Cause.Interrupt`), it will **not** be retried. This ensures that logic errors and cancellation signals are respected immediately.

```fsharp
let unstableCall = 
    flow {
        printfn "Attempting call..."
        return! Flow.fail "temporary-error"
    }

// This will attempt the call up to 4 times (initial + 3 retries)
let resilientCall = 
    Flow.Retry(unstableCall, Schedule.recurs 3)
```

## Repeating Successes

Use `Flow.Repeat` to execute a successful flow again. This is useful for polling, heartbeats, or recurring background tasks.

```fsharp
let pollStatus = 
    flow {
        printfn "Polling status..."
        return "Still working"
    }

// This will poll every 5 seconds until it fails or is cancelled
let recurringPoll = 
    Flow.Repeat(pollStatus, Schedule.spaced (TimeSpan.FromSeconds 5.0))
```

## API Reference: Module `Schedule`

| Function | Signature | Description |
| :--- | :--- | :--- |
| `recurs` | `int -> Schedule<'env, 'i, int>` | Recurs exactly `n` times. The output value is the attempt index. |
| `spaced` | `TimeSpan -> Schedule<'env, 'i, int>` | Recurs indefinitely with a fixed delay. |
| `exponential` | `TimeSpan -> Schedule<'env, 'i, TimeSpan>` | Recurs indefinitely with doubling delays. |
| `jittered` | `Schedule<'env, 'i, 'o> -> Schedule<'env, 'i, 'o>` | Wraps a schedule to add random jitter (0.5x to 1.5x). |

## API Reference: `Flow` Extensions

| Function | Signature | Description |
| :--- | :--- | :--- |
| `Flow.Retry` | `Flow<'e, 'err, 'v> * Schedule<'e, 'err, 'o> -> Flow<'e, 'err, 'v>` | Retries the flow on `Cause.Fail`. |
| `Flow.Repeat` | `Flow<'e, 'err, 'v> * Schedule<'e, 'v, 'o> -> Flow<'e, 'err, 'v>` | Repeats the flow on success. |
