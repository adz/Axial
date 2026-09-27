---
title: Schedules
description: "Combined schedules drive retry, repeat, and supervise concurrently, and timeouts race operations of random length."
---

# Schedules

This scenario checks that [schedules](/scheduling-and-retries/index.html) run exactly as many times as their rules say,
and that timeouts and supervision end cleanly.

Run it with `dotnet run --project examples/Axial.TortureTest -- schedules 100`.

## What it does

- Forty concurrent cases retry a flow that always fails, under schedules built from random counts with `recurs`,
  `recursAtMost`, `union`, `intersect`, `andThen`, `whileOutput`, `untilOutput`, `resetAfter`, and `jitteredWith`.
  Each also streams the schedule with `FlowStream.fromSchedule`.
- `whileInput` and `untilInput` retry only some errors, and `Retry.schedule` builds schedules from `Retry` records.
- A `fixedRate` repeat records when each run starts, measured in real time.
- Thirty workers die a random number of times under `Flow.supervise`. A typed failure and a hung flow run under it too.
- Sixty operations of random length race `Flow.timeout`, `timeoutToError`, `timeoutToOk`, and `timeoutWith`.

{{< snippet id="torture-schedules" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| Every combined schedule ran exactly as many times as its rules say | The combinators compose as documented: `union` runs while either side continues, `intersect` while both do, `andThen` runs one after the other. |
| whileInput retried the transient errors and stopped at the fatal one | A schedule can decide by the error it is given. |
| untilInput stopped at the fatal error | The same, stopping at the first match. |
| Retry records retried as configured | `Retry.schedule` honours `Retries`, `Backoff`, and `When`. |
| A fixed-rate repeat never started a run before its tick | `fixedRate` runs on a fixed grid from its start and never early. |
| Time budgets stopped the schedule, and elapsed time never went backwards | `upTo` stops on time, and `Schedule.elapsed` is monotonic. |
| supervise restarted every defect while the schedule allowed, and no more | `Flow.supervise` restarts after a defect as long as its schedule continues. |
| A typed failure was not restarted | Supervision is for defects; a typed failure is returned. |
| An interruption was not restarted | Interrupting a supervised flow stops it. |
| Every timeout returned the value or its fallback, after its operation had settled | A timeout interrupts its operation and waits for it to finish before returning, so the operation's cleanup has run. |
