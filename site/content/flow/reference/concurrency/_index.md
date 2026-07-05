---
title: "Concurrency"
weight: 50
type: docs
---

This page shows the small Flow-native concurrency primitives added for coordination that needs Axial semantics rather than raw .NET behavior. `Deferred<'error, 'value>` is a one-shot typed handoff point backed by a full `Exit<'value, 'error>`. `FlowSemaphore` limits concurrent workflow sections through scoped `Semaphore.withPermit`, releasing permits after success, typed failure, defect, or interruption.

## Deferred

- [`Flow.Deferred`](./t-flow-deferred.md):
 A one-shot, typed handoff point that can be completed exactly once with a full <a href="https://learn.microsoft.com/dotnet/api/axial.exit-2">Exit</a>.

- [`Flow.Deferred.make`](./m-flow-deferred-make.md): Creates an empty deferred value.
- [`Flow.Deferred.await`](./m-flow-deferred-await.md): Waits for the deferred outcome, preserving success, typed failure, defect, or interruption.
- [`Flow.Deferred.complete`](./m-flow-deferred-complete.md): Attempts to complete the deferred value with a full outcome.
- [`Flow.Deferred.succeed`](./m-flow-deferred-succeed.md): Attempts to complete the deferred value successfully.
- [`Flow.Deferred.fail`](./m-flow-deferred-fail.md): Attempts to complete the deferred value with a typed failure.
- [`Flow.Deferred.die`](./m-flow-deferred-die.md): Attempts to complete the deferred value with a defect.
- [`Flow.Deferred.interrupt`](./m-flow-deferred-interrupt.md): Attempts to complete the deferred value as interrupted.

## Semaphore

- [`Flow.FlowSemaphore`](./t-flow-flowsemaphore.md): A Flow-native semaphore handle used to limit concurrent workflow sections.
- [`Flow.Semaphore.make`](./m-flow-semaphore-make.md): Creates a semaphore with the supplied initial permit count.
- [`Flow.Semaphore.create`](./m-flow-semaphore-create.md): Alias for <code>make</code>.
- [`Flow.Semaphore.withPermit`](./m-flow-semaphore-withpermit.md): Runs a workflow while holding one permit and always releases the permit afterward.
