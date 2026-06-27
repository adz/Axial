---
weight: 12
title: Deferred and Semaphore
description: One-shot typed coordination and scoped concurrency limits.
aliases:
  - /docs/state-concurrency/deferred-semaphore/
type: docs
---


Axial includes a small set of concurrency primitives only where they add Axial semantics over the .NET primitives underneath.

Use .NET `Task`, `Channel<T>`, `SemaphoreSlim`, and `ConcurrentQueue<T>` directly when raw platform behavior is enough. Use Axial primitives when coordination should preserve typed `Exit` and `Cause`, participate in workflow interruption, or release resources through the `Flow` model.

## Deferred

`Deferred<'error, 'value>` is a one-shot handoff point between fibers. It can be completed once with a full `Exit<'value, 'error>`, so success, typed failure, defects, and interruption all remain visible to waiters.

Completion operations are idempotent. They return `true` to the caller that completed the deferred value and `false` to later callers.

```fsharp
let handoff : Flow<unit, string, int> =
    flow {
        let! deferred = Deferred.make<unit, string, int> ()

        let! waiter =
            Deferred.await deferred
            |> Flow.fork

        let! completed = Deferred.succeed 42 deferred
        let! value = Flow.join waiter

        if completed then
            return value
        else
            return! Flow.fail "deferred was already completed"
    }
```

Use `Deferred` when a fiber needs to wait for a typed outcome produced elsewhere:

- `Deferred.await` waits for the outcome and resumes with the same success or failure.
- `Deferred.complete` completes with a full `Exit`.
- `Deferred.succeed`, `Deferred.fail`, `Deferred.die`, and `Deferred.interrupt` complete common outcomes directly.

Awaiting respects runtime cancellation. If the waiting workflow is interrupted before the deferred value is completed, the await returns `Cause.Interrupt`.

## Semaphore

`FlowSemaphore` limits how many workflows can enter a section at the same time. The public API is intentionally scoped: use `Semaphore.withPermit` instead of raw acquire/release.

```fsharp
let limitedFetch semaphore request =
    Semaphore.withPermit semaphore (
        flow {
            // Only one workflow per permit can run this section.
            return! runRequest request
        })
```

`Semaphore.withPermit` releases the permit after success, typed failure, defect, or interruption. This is the important difference from manually calling `WaitAsync` and `Release`: permit cleanup follows the workflow outcome.

Create semaphores with a positive permit count:

```fsharp
let program : Flow<unit, string, unit> =
    flow {
        let! semaphore = Semaphore.make 4
        do! Semaphore.withPermit semaphore doWork
    }
```

Zero permits are rejected because Axial does not expose an external raw release operation. A semaphore created with zero permits would be a permanently blocked handle rather than a useful concurrency limit.

## Queues

Axial does not currently expose a queue primitive. A useful Axial queue needs more than a thin wrapper over `Channel<T>`: bounded strategy, shutdown, blocked offerer/taker interruption, fairness, and resource cleanup all need explicit semantics.

Until a v1 feature needs those semantics, use .NET channels directly at the edge of a workflow and convert operations into `Flow` where needed.
