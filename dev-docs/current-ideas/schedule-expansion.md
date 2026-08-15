# Schedule expansion: deferred post-1.0 decisions

This is the follow-up to item 1 ("Stabilize the schedule contract") in `api-review.md`. That item is now closed:
every semantic question on its checklist was already correctly implemented, just undocumented and untested. Pinning
tests now cover `recurs` count semantics, schedule statelessness/reusability, overflow and negative-delay rejection,
jitter clamping for out-of-contract samples, and retry/repeat behavior on defects and interruption (see
`tests/Axial.Tests/WorkflowSchedulingTests.fs`), and `Schedule.fs`'s doc comments now state each guarantee. None of
that required changing the `Schedule` type's shape, so it did not block a 1.0 freeze.

Two questions came up during that review that are genuinely open and do not need to be answered before 1.0. `Schedule`
is a stateless decision function; adding combinators or new schedule constructors to it later cannot break existing
schedules, so there is no forced-now cost like there was with `Ref.modify`'s tuple order. Recording them here so they
aren't lost.

## 1. Composition surface

The current surface is deliberately minimal: `recurs`, `spaced`, `exponential`, `jitteredWith`. There is no way to:

- combine two schedules (e.g. stop when either one would stop — `Schedule.union`/`Schedule.both` in ZIO's terms);
- sequence schedules (e.g. three fast retries, then fall back to exponential backoff — `Schedule.andThen`);
- gate recurrence on the flow's output or error value (`Schedule.whileOutput`, `Schedule.recurUntil`).

Decide whether real usage surfaces a need for any of these before adding them. Retrofitting a combinator later is
additive and safe; do not add one speculatively.

## 2. Elapsed-time tracking

No built-in schedule currently exposes cumulative elapsed time. There's no way to express "retry with backoff, but
give up after 5 minutes total" without hand-rolling it against `DateTimeOffset.UtcNow` inside the retried flow itself.
A `Schedule.upTo (TimeSpan)` or an `elapsed` combinator would need to fit the existing `'input -> int -> Flow<'env,
unit, 'output option * TimeSpan>` shape, which forwards individual attempt delays, not affinity to a wall-clock
target — the current output slot (`'output`) or the schedule's internal closure would need to carry the running
total.

Decide whether this is common enough in practice to justify the added surface, or better left to application code
composing `Schedule.retry`/`Schedule.repeat` with an existing timeout combinator (`Flow.Runtime.timeout`).

## Non-decisions from the original review item

For reference, these were on the original checklist and turned out to already be correctly implemented — no design
work needed, only tests and documentation:

- `Schedule.recurs n` counts additional retries/repeats on top of the source flow's one free initial attempt, not
  total executions.
- A `Schedule` value is stateless and safely reusable; attempt counting lives in the `retry`/`repeat` call.
- `spaced`/`exponential` reject negative delays via `invalidArg`; `exponential`/`jitteredWith` cap at
  `TimeSpan.MaxValue` instead of overflowing.
- `jitteredWith` never throws for an out-of-contract `sample()` — it clamps instead.
- `Schedule.retry` only retries `Cause.Fail`; defects and interruptions propagate immediately.
- `Schedule.repeat` only repeats on success; any failure propagates immediately.
