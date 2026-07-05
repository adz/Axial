---
title: "Schedule"
weight: 60
type: docs
---

This page shows the `Schedule` surface for describing retry and repeat policies as values. A schedule decides when a workflow should run again, what delay should be used, and what output should be accumulated for each step. Use schedules when retry behavior is part of the workflow boundary and must stay explicit, testable, and separate from the domain operation being retried. The common entry points are `recurs` for bounded repetition, `spaced` for fixed delays, `exponential` for backoff, `jittered` when several callers should not retry in lockstep, `retry` for typed failures, and `repeat` for successful values.

## Core type

- [`Flow.Schedule`](./t-flow-schedule.md):  Represents a stateful schedule that can decide whether to continue and how long to delay.

## Module functions

- [`Flow.Schedule.recurs`](./m-flow-schedule-recurs.md): Creates a schedule that recurs a fixed number of times.
- [`Flow.Schedule.spaced`](./m-flow-schedule-spaced.md): Creates a schedule that recurs with a fixed delay between attempts.
- [`Flow.Schedule.exponential`](./m-flow-schedule-exponential.md): Creates a schedule that recurs with exponential backoff.
- [`Flow.Schedule.jittered`](./m-flow-schedule-jittered.md): Adds random jitter to a schedule&#39;s delay.
- [`Flow.Schedule.retry`](./m-flow-schedule-retry.md): Retries a failing flow according to the supplied schedule.
- [`Flow.Schedule.repeat`](./m-flow-schedule-repeat.md): Repeats a successful flow according to the supplied schedule.
