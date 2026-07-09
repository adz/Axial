---
title: "Schedule"
weight: 100
---

This page shows the `Schedule` surface for describing retry and repeat policies as values. A `Schedule` on its own does nothing — it is a definition of when to run again (recur or stop) and how long to wait, not an action. Build one with `recurs` (bounded repetition), `spaced` (fixed delay), `exponential` (backoff), and `jittered` (randomized delay, so callers don't retry in lockstep), then apply it to a flow with `Schedule.retry` (rerun on a typed failure) or `Schedule.repeat` (rerun on a success) — nothing happens until one of those two runs the schedule against an actual workflow. Use schedules when retry behavior is part of the workflow boundary and must stay explicit, testable, and separate from the domain operation being retried.

## Core type

- [`Flow.Schedule`](./t-flow-schedule.md):  Represents a stateful schedule that can decide whether to continue and how long to delay.

## Module functions

- [`Flow.Schedule.recurs`](./m-flow-schedule-recurs.md): Creates a schedule that recurs a fixed number of times.
- [`Flow.Schedule.spaced`](./m-flow-schedule-spaced.md): Creates a schedule that recurs with a fixed delay between attempts.
- [`Flow.Schedule.exponential`](./m-flow-schedule-exponential.md): Creates a schedule that recurs with exponential backoff.
- [`Flow.Schedule.jittered`](./m-flow-schedule-jittered.md): Adds random jitter to a schedule&#39;s delay.
- [`Flow.Schedule.retry`](./m-flow-schedule-retry.md): Retries a failing flow according to the supplied schedule.
- [`Flow.Schedule.repeat`](./m-flow-schedule-repeat.md): Repeats a successful flow according to the supplied schedule.
