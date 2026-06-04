---
title: "Schedule"
weight: 100
---

This page shows the `Schedule` surface for describing retry and repeat policies as values. A schedule decides when a workflow should run again, what delay should be used, and what output should be accumulated for each step. Use schedules when retry behavior is part of the workflow boundary and must stay explicit, testable, and separate from the domain operation being retried. The common entry points are `recurs` for bounded repetition, `spaced` for fixed delays, `exponential` for backoff, and `jittered` when several callers should not retry in lockstep.

## Core type

- [`Schedule`](./t-schedule.md):

## Module functions

- [`Schedule.recurs`](./m-schedule-recurs.md): Creates a schedule that recurs a fixed number of times.
- [`Schedule.spaced`](./m-schedule-spaced.md): Creates a schedule that recurs with a fixed delay between attempts.
- [`Schedule.exponential`](./m-schedule-exponential.md): Creates a schedule that recurs with exponential backoff.
- [`Schedule.jittered`](./m-schedule-jittered.md): Adds random jitter to a schedule&#39;s delay.
