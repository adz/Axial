---
title: "Schedule"
type: docs
---

The `Schedule` module provides a DSL for describing execution policies.

## Core type

- [`FsFlow.Schedule`](./t-schedule.md): Represents a stateful schedule that can decide whether to continue and how long to delay.

## Module functions

- [`FsFlow.ScheduleModule.recurs`](./m-schedulemodule-recurs.md): Creates a schedule that recurs a fixed number of times.
- [`FsFlow.ScheduleModule.spaced`](./m-schedulemodule-spaced.md): Creates a schedule that recurs with a fixed delay between attempts.
- [`FsFlow.ScheduleModule.exponential`](./m-schedulemodule-exponential.md): Creates a schedule that recurs with exponential backoff.
- [`FsFlow.ScheduleModule.jittered`](./m-schedulemodule-jittered.md): Adds random jitter to a schedule's delay.

## Flow extensions

- [`FsFlow.FlowScheduleExtensions.Flow.Retry.Static`](./m-flowscheduleextensions-flow-retry-static.md): Retries a failing flow according to the supplied schedule.
- [`FsFlow.FlowScheduleExtensions.Flow.Repeat.Static`](./m-flowscheduleextensions-flow-repeat-static.md): Repeats a successful flow according to the supplied schedule.

