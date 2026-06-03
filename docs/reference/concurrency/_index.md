---
title: "Concurrency"
weight: 500
---

This page shows the small Flow-native concurrency primitives added for coordination that needs FsFlow semantics rather than raw .NET behavior. `Deferred<'error, 'value>` is a one-shot typed handoff point backed by a full `Exit<'value, 'error>`. `FlowSemaphore` limits concurrent workflow sections through scoped `Semaphore.withPermit`, releasing permits after success, typed failure, defect, or interruption.

## Deferred

- [`Deferred`](./t-deferred.md):
 A one-shot, typed handoff point that can be completed exactly once with a full <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a>.

- [`Deferred.make`](./m-deferred-make.md): Creates an empty deferred value.
- [`Deferred.await`](./m-deferred-await.md): Waits for the deferred outcome, preserving success, typed failure, defect, or interruption.
- [`Deferred.complete`](./m-deferred-complete.md): Attempts to complete the deferred value with a full outcome.
- [`Deferred.succeed`](./m-deferred-succeed.md): Attempts to complete the deferred value successfully.
- [`Deferred.fail`](./m-deferred-fail.md): Attempts to complete the deferred value with a typed failure.
- [`Deferred.die`](./m-deferred-die.md): Attempts to complete the deferred value with a defect.
- [`Deferred.interrupt`](./m-deferred-interrupt.md): Attempts to complete the deferred value as interrupted.

## Semaphore

- [`FlowSemaphore`](./t-flowsemaphore.md): A Flow-native semaphore handle used to limit concurrent workflow sections.
- [`Semaphore.make`](./m-semaphore-make.md): Creates a semaphore with the supplied initial permit count.
- [`Semaphore.create`](./m-semaphore-create.md): Alias for <code>make</code>.
- [`Semaphore.withPermit`](./m-semaphore-withpermit.md): Runs a workflow while holding one permit and always releases the permit afterward.
