---
title: Guarantees
description: What Axial promises about finished work, interruption, defects, and shutdown, in plain words.
---

# Guarantees

One rule runs through every concurrency construct in Axial:

**When a flow returns, everything it started has finished and released its resources.** "Stopped" means the port,
pipe, or socket is free; "done" means locks are released and child processes have exited. The only exception is
work you explicitly hand off with `Flow.disconnect`, and even that work belongs to the application, which waits for it
at shutdown.

The sections below spell out what that means for each construct, and the few places where you choose otherwise.

## Waiting for work that was stopped

| Construct | What happens to the other work | When it returns |
|---|---|---|
| `Flow.race` | The loser is interrupted. | After the loser has settled and its scope has closed. |
| `Flow.timeout` and variants | The timed flow is interrupted. | After that flow's cleanup; only then is the timeout error, value, or fallback produced. |
| `Flow.zipPar`, `sequencePar`, `traversePar` | On the first failure, running siblings are interrupted. | After every sibling has settled. |
| `Flow.fork` | The child belongs to the forking scope. | `fork` returns at once; the scope's close interrupts and waits for the child. |
| `Flow.forkReplacing`, `forkReplacingKey` | The previous fiber in the slot is interrupted. | The new flow starts only after the previous fiber has settled, so the two never overlap. |
| `Fiber.interrupt`, `FiberSlot.interrupt` | The fiber is interrupted. | After the fiber has settled and its scope has closed. |
| `FlowStream` terminals | Producers started by `buffer`, `mergePar`, and time operators are interrupted when the consumer stops. | After those producers have settled. |
| `Process` runs | Pumps, stdin feeders, and the stop coordinator are stopped. | After the process has exited and every feeder and pump has settled. |

A flow that ignores interruption keeps its owner waiting. That is deliberate: returning early would let the old work
overlap whatever comes next, such as a restarted server binding the same port.

## Choosing not to wait

These names say that they do not wait, or do not consume an outcome:

- `Flow.disconnect`: interrupting the caller returns at once while the source keeps settling in its own fiber. Use it
  for foreign operations that may ignore cancellation, such as a stdin source that blocks in non-cancellable code.
  The detached fiber belongs to the root run (`Flow.run`, `App.start`), which interrupts it and waits for it when the
  application finishes.
- `Fiber.requestInterrupt`: asks a fiber to stop and returns without waiting.
- `Flow.forkDetached`: a child whose defects are not reported because nothing will read them. Its scope still waits for it.
- `IDisposable.Dispose` on an `AppHandle`: requests stop; await `Stop()` or `Completion` to wait.

## Interruption

Cancelling the runtime token produces `Cause.Interrupt`. Only the code that asked for the interruption may turn it
into a value: `Flow.timeout` turns its own interruption of the timed flow into a timeout error, and
`Fiber.interrupt` returns the interrupted fiber's `Exit`. Everywhere else, interruption passes through unchanged.

Typed-error handlers (`Flow.recover`, `Flow.orElseWith`, `Flow.fold`, `Flow.retry`) never see interruption or defects.
`Flow.foldCause` sees everything and can swallow both, so its handler must re-raise `Cause.Interrupt` unless it owns
the cancellation, and should re-raise or report every `Cause.Die`.

## Defects are never lost

A defect is an unexpected exception: a bug, not a typed error. Every defect is either part of a returned cause or
reported once through the fiber observer (`Flow.withFiberObserver`, `OnUnobservedDefect`). Never both, never neither.

- A race loser's defect is combined into the race's result.
- A defect in a fiber whose outcome nobody consumes (a discarded `fork` handle, a replaced or slot-interrupted fiber,
  disconnected work) is reported once.
- In `FlowStream.mergePar`, a defect from a stream stopped by another stream's failure is combined into that failure,
  or reported if it arrives after the failure was delivered.
- In `Process`, a stdin source's failure or defect is part of the run's result. Interruption, and the pipe-closed
  errors caused by Axial closing the pipe, are not failures.

## Process runs

A process run returns only after the process has exited and every stdin feeder and output pump has settled:

- When the program exits or stops reading, Axial interrupts the stdin feeder and closes the pipe, then waits for the
  feeder. A failure or defect the feeder settles with is the run's failure.
- A stdin source that ignores interruption keeps the run waiting. Wrap that source in `Flow.disconnect` to let the run
  return without it.
- `Process.stopGracefully` waits the full grace you configure at each step; no fixed internal limit cuts a stop
  ladder short.

## Shutdown

`AppHandle.Stop()` and `Completion` wait for the root flow, its scope finalizers, and any disconnected work.
In a Generic Host, `StopAsync` waits for the same. If the host's own shutdown timeout expires first, it logs a
warning that cleanup is still running instead of abandoning it silently.

## Testing these guarantees

The [torture tests](torture-tests/index.html) exercise each construct under interruption, failure, and shutdown.
When you write your own tests, wait for facts rather than time: complete a `Deferred` or `TaskCompletionSource` when
work reaches the point you care about, and wait for that. A sleep that "gives work time" to reach a state passes on a
quiet machine and fails under load.
