---
title: Deferred and Semaphore
description: One-shot typed coordination and scoped concurrency limits.
---

# Deferred and Semaphore

Axial includes a small set of concurrency primitives only where they add Axial semantics over the .NET primitives underneath.

Use .NET `Task`, `Channel<T>`, `SemaphoreSlim`, and `ConcurrentQueue<T>` directly when raw platform behavior is enough. Use Axial primitives when coordination should preserve typed `Exit` and `Cause`, participate in workflow interruption, or release resources through the `Flow` model.

## Deferred

`Deferred<'error, 'value>` is a one-shot handoff point between fibers. It can be completed once with a full `Exit<'value, 'error>`, so success, typed failure, defects, and interruption all remain visible to waiters.

Completion operations are idempotent. They return `true` to the caller that completed the deferred value and `false` to later callers.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
let handoff : Flow<unit, string, int> =
    flow {
        let! deferred = Deferred.make<unit, string, int> ()

        let! waiter =
            Deferred.await deferred
            |> Flow.fork

        let! completed = Deferred.succeed 42 deferred
        let! value = Fiber.join waiter

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
let active = ref 0
let busiest = ref 0

let runRequest (request: int) : Flow<string, int> =
    flow {
        let now = Interlocked.Increment &active.contents
        lock busiest (fun () -> busiest.Value <- max busiest.Value now)
        do! Flow.sleep (TimeSpan.FromMilliseconds 5.0)
        Interlocked.Decrement &active.contents |> ignore
        return request * 10
    }

let limitedFetch (semaphore: FlowSemaphore) (request: int) : Flow<string, int> =
    Semaphore.withPermit semaphore (
        flow {
            // Only one workflow per permit can run this section.
            return! runRequest request
        })
```

`Semaphore.withPermit` releases the permit after success, typed failure, defect, or interruption. This is the important difference from manually calling `WaitAsync` and `Release`: permit cleanup follows the workflow outcome.

Create semaphores with a positive permit count:

```fsharp
let program : Flow<unit, string, int list> =
    flow {
        let! semaphore = Semaphore.make 2
        return! [ 1..8 ] |> Flow.traversePar (Parallelism.bounded 8) (limitedFetch semaphore)
    }
```

```fsharp run
program |> Flow.run () |> shouldEqual (Exit.Success [ 10; 20; 30; 40; 50; 60; 70; 80 ])
busiest.Value <= 2 |> shouldEqual true
```

Eight workers ran, but never more than the two permits at once.

Zero permits are rejected because Axial does not expose an external raw release operation. A semaphore created with zero permits would be a permanently blocked handle rather than a useful concurrency limit.

## Queues

To hand a stream of values between fibers, use [Queue](queue.html). It adds bounded, dropping, and sliding strategies,
a shutdown that lets the consumer drain its backlog, and interruption that never loses or duplicates a value.

The [semaphore](torture-tests/semaphores.html) and [deferred](torture-tests/deferreds.html) torture tests interrupt
waiters and holders at random and check that no permit is lost and exactly one completion wins.
