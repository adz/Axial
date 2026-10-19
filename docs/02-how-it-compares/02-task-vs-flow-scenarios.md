---
title: "Task vs Flow: seven scenarios"
description: Compare the same application operations written with Task and Flow, including the tests that prove each behavior.
---

# Task vs Flow: seven scenarios

This page compares seven application operations implemented twice: once with `Task`, exceptions, cancellation tokens,
and manually passed services; once with `Flow<'env, 'error, 'value>`. Both versions use the same domain types and
service interfaces.

Flow does not remove side effects or make remote operations transactional. It gives each operation a compatible
contract and runtime model. The type records the capabilities it needs and expected failures it can return. The runtime
owns cancellation, child work, and scoped cleanup. Retry, timeout, parallelism, and recovery are policies you compose
with the operation instead of conventions each caller must reproduce.

The [comparison test file](https://github.com/adz/Axial/blob/main/tests/Axial.Comparisons.Tests/ComparisonTests.fs)
tests each scenario's behavior. Each scenario identifies what the type communicates, what the runtime guarantees, and
what your application must still handle. ZIO concepts are named alongside their Axial counterparts.

| Scenario | Type makes visible | Runtime enforces |
| --- | --- | --- |
| [1. Checkout orchestration](#1-checkout-orchestration-with-compensation) | required services and the complete failure set | release runs on every exit shape, including defects |
| [2. Resilient HTTP with a retry budget](#2-resilient-http-call-with-a-retry-budget) | transient/terminal error taxonomy and both capabilities | only predicate-accepted typed failures retry; the timeout reaches in-flight work |
| [3. Parallel dashboard fan-out](#3-parallel-dashboard-fan-out) | which branch may fail silently | loser interruption and cause merging |
| [4. Scoped temporary workspace](#4-scoped-temporary-workspace) | acquisition and finalization as one signature | the finalizer runs on every exit shape and its failure is preserved |
| [5. Application wiring](#5-application-wiring-that-cannot-omit-a-capability) | the full capability set | nothing — the compiler checked it |
| [6. Producer/consumer pipeline](#6-producerconsumer-pipeline-with-backpressure-and-interruption) | stream failure and environment requirements stay typed | pull-based evaluation stops the producer with the consumer |
| [7. Atomic inventory reservation](#7-atomic-inventory-reservation-under-contention) | `STM` as a transaction value, separate from effects | all-or-nothing commit; coherent-snapshot retry |

Each row links to its full comparison below.

A self-contained pair, checked here: a lookup that fails the same way in both models, but only `Flow` names the
failure in the type.

```fsharp prepare
open System
open System.Threading.Tasks
open Axial

let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
type UserId = UserId of int
type LookupError = | Missing of int

let directory = Map [ (1, "Ada"); (2, "Grace") ]

// Task: a missing user is an exception the signature does not name.
let taskLookup (UserId id) : Task<string> =
    task {
        match Map.tryFind id directory with
        | Some name -> return $"Hello, {name}"
        | None -> return raise (InvalidOperationException($"no user {id}"))
    }

// Flow: the same failure is part of the type.
let flowLookup (UserId id) : Flow<unit, LookupError, string> =
    flow {
        match Map.tryFind id directory with
        | Some name -> return $"Hello, {name}"
        | None -> return! Flow.fail (Missing id)
    }
```

```fsharp run
taskLookup (UserId 1) |> fun (t: Task<string>) -> t.GetAwaiter().GetResult() |> shouldEqual "Hello, Ada"
flowLookup (UserId 1) |> Flow.run () |> shouldEqual (Exit.Success "Hello, Ada")
flowLookup (UserId 99) |> Flow.run () |> shouldEqual (Exit.Failure (Cause.Fail (Missing 99)))
```

## 1. Checkout orchestration with compensation

Reserve stock, charge a card, create a shipment; release the reservation when charging or shipment creation fails.

Read the [checkout comparison source](https://github.com/adz/Axial/blob/main/examples/Axial.Comparisons/CheckoutCompensation.fs).

The ordinary version is a `Task<CheckoutReceipt>` whose expected failures leave the signature as exceptions, and
whose compensation lives in a catch block. The comparison includes `checkoutBuggy`, a failure branch added outside
the `try`, and a test proving the reservation leaks.

{{< snippet id="compare-checkout" mode="no-check" reason="Excerpt from examples/Axial.Comparisons, whose tests compile and run it" >}}

Failures enter `CheckoutError` at each bind site with
[`Bind.mapError`](/error-handling/bind.html). The Bind guide explains why the same mapping syntax works for
`Result`, asynchronous results, and Flow sources. The release action is attached to the reservation's lexical
lifetime, so it runs on success, typed failure, defect (a test throws from the shipping
adapter), and interruption. ZIO correspondence: environment services, typed errors, `acquireRelease`.

- **Made visible by the type**: the required services (`CheckoutEnv`) and the complete failure set (`CheckoutError`).
- **Enforced by the runtime**: release runs on every exit shape, including defects the catch-based version can miss.
- **Still the application's responsibility**: remote compensation is not transactional; idempotency keys and
  reconciliation are still required.

## 2. Resilient HTTP call with a retry budget

Fetch an exchange rate through [`Axial.HttpClient`](/http/): retry only transient
transport failures, back off exponentially, stop after three attempts, and turn a two-second deadline into
`RateError.TimedOut`. Never retry malformed successful responses.

Read the [retry-budget comparison source](https://github.com/adz/Axial/blob/main/examples/Axial.Comparisons/RetryBudget.fs).

The ordinary version interleaves a retry loop, `CancellationTokenSource.CancelAfter`, `Task.Delay`, and exception
classification in one function, and a broad `with _ ->` would also retry a `NullReferenceException`.

{{< snippet id="compare-retry" mode="no-check" reason="Excerpt from examples/Axial.Comparisons, whose tests compile and run it" >}}

Retry and timeout are policies applied to a cold workflow from outside. The retry predicate selects typed failures;
defects and interruption are never retried, because `Flow.retry` re-runs `Cause.Fail` only. The
tests pin all four behaviors: recovery within the budget, budget exhaustion, no retry of `Malformed`, and the
timeout interrupting a hung request. ZIO correspondence: `timeoutFail`, typed `Schedule`, `retry`.

- **Made visible by the type**: the transient/terminal error taxonomy and both required capabilities: `IHasHttp`
  for the request and `IHasClock` for retry delays and the two-second timeout. The underlying request needs only
  `IHasHttp`; the composed operation needs an environment implementing both interfaces.
- **Enforced by the runtime**: only predicate-accepted typed failures are retried; the timeout reaches sleeps and
  the in-flight request through the ambient cancellation token.
- **Still the application's responsibility**: the operation must be safe to repeat, and the adapter must not hide a
  clock or other effect.

## 3. Parallel dashboard fan-out

Load account, orders, and recommendations concurrently. Account and orders are mandatory; recommendations fall back
to an empty list on their typed failure; a mandatory failure interrupts the still-running sibling.

Read the [dashboard comparison source](https://github.com/adz/Axial/blob/main/examples/Axial.Comparisons/DashboardFanOut.fs).

{{< snippet id="compare-dashboard" mode="no-check" reason="Excerpt from examples/Axial.Comparisons, whose tests compile and run it" >}}

The ordinary `Task.WhenAll` version must cancel siblings by hand through a linked token source, and two simultaneous
failures surface as whichever exception `WhenAll` publishes first. With `zipPar` the runtime does the interruption:
the test's slow sibling awaits `Task.Delay` on its runtime token and asserts that it observed the resulting
`OperationCanceledException`. Concurrent failures merge as `Cause.Both`. First-success semantics
are a different contract, so they appear as a separate `Flow.race` example rather than a subtle change to this one.
ZIO correspondence: `zipPar`, typed `catchAll` (here `orElse`), `race`.

- **Made visible by the type**: which branch may fail silently (none: the fallback is explicit at the composition).
- **Enforced by the runtime**: loser interruption and cause merging.
- **Still the application's responsibility**: Flow cannot prove an arbitrary adapter honours cancellation, and an
  adapter that registers its own extra observer on the runtime token, instead of catching the cancellation that the
  operation it awaits throws, can miss the interrupt; see
  [Task and Async Interop](../the-flow-type/task-async-interop.html#pitfall-dont-register-a-second-cancellation-observer-on-the-same-token).

## 4. Scoped temporary workspace

Create a temporary directory through [`Axial.FileSystem`](/services/filesystem.html), perform fallible
steps, remove the directory exactly once on every exit shape.

Read the [workspace comparison source](https://github.com/adz/Axial/blob/main/examples/Axial.Comparisons/ScopedWorkspace.fs).

The ordinary comparison includes `importBatchLeaky`: construction succeeds, then a setup check
throws *before* ownership transfers into `try/finally`. The test proves the directory survives. In the Flow version
there is no such gap: `Flow.scopeAcquireRelease` owns the resource from the instant acquisition succeeds, and
`Flow.scoped` keeps the failing gate inside the resource's lifetime:

{{< snippet id="compare-workspace" mode="no-check" reason="Excerpt from examples/Axial.Comparisons, whose tests compile and run it" >}}

ZIO correspondence: `Scope` and `ZIO.acquireRelease`; for resources that should live as long as a provided layer,
Axial has `Flow.scopeAcquireRelease` and `Layer.acquireRelease`. One difference: a forked fiber in Axial owns its own
scope, like `ZIO.scoped(effect).fork`, so use `Flow.forkResource` where ZIO code would `fork` an `acquireRelease` and
`join` it later (see the [Scope reference](/api/Axial.Scope.html#scope-transfer-per-combinator)).

- **Made visible by the type**: acquisition and finalization form one construct with one signature.
- **Enforced by the runtime**: the finalizer runs on success, typed failure, defect, and interruption, and a
  finalizer failure is preserved in the cause rather than replacing the primary outcome.
- **Still the application's responsibility**: choosing the correct scope; keeping finalizers idempotent.

## 5. Application wiring that cannot omit a capability

A daily report needs a clock, a filesystem, a console, and a report store. The ordinary version threads four
constructor parameters through every caller, and callers can instead read
`DateTimeOffset.UtcNow` directly.

Read the [report-wiring comparison source](https://github.com/adz/Axial/blob/main/examples/Axial.Comparisons/ReportWiring.fs).

The Flow version declares the capability set once as an environment record implementing one contract per
capability, and business code names only what it uses through the package operations
([`Clock.now`](/services/platform-services/clock.html), [`FileSystem.readAllText`](/services/filesystem.html),
[`Console.writeLine`](/services/console.html)):

{{< snippet id="compare-report" mode="no-check" reason="Excerpt from examples/Axial.Comparisons, whose tests compile and run it" >}}

The test builds the same record from `Clock.fromValue` and in-memory doubles and asserts a deterministic report
name, with no time-mocking framework or service locator. Reading the clock without declaring it does not compile.
ZIO correspondence: environment requirements and `ZLayer`.

- **Made visible by the type**: the full capability set; a missing capability is a compile error at the edge.
- **Enforced by the runtime**: nothing, because the compiler already checked it.
- **Still the application's responsibility**: the type proves presence and shape, not configuration quality.

## 6. Producer/consumer pipeline with backpressure and interruption

Stream records (or live process output), transform them, persist them, and stop the producer promptly when the
consumer fails.

Read the [output-pipeline comparison source](https://github.com/adz/Axial/blob/main/examples/Axial.Comparisons/OutputPipeline.fs).

The ordinary version combines a bounded `Channel`, a background producer task, a linked token source, and manual
observation of the producer's exception. Without `linked.Cancel()`, a consumer failure can return while the producer keeps writing.

A `FlowStream` is cold and pull-based: when persistence fails, the consumer stops pulling. The test
streams from an instrumented infinite sequence and asserts almost nothing was produced past the failing element.
The process variant uses [`Process.streamStdoutLines`](/process/), which streams decoded stdout lines from a live
process through the same pipeline shape:

{{< snippet id="compare-process-output" mode="no-check" reason="Excerpt from examples/Axial.Comparisons, whose tests compile and run it" >}}

Where a producer must genuinely run ahead, `Flow.fork` returns a `Fiber` the caller owns and must `join` or
`interrupt`. ZIO correspondence: `ZStream`, scoped fibers, interruption.

- **Made visible by the type**: stream failure and environment requirements stay typed through the pipeline; a
  forked producer is a value someone must own.
- **Enforced by the runtime**: pull-based evaluation stops the producer with the consumer; fiber interruption
  reaches sleeps.
- **Still the application's responsibility**: the buffering policy (`Process.streamStdoutLines` delivery is bounded;
  document your own), and never detaching a fiber without an owner.

## 7. Atomic inventory reservation under contention

Two checkouts race for the last unit; a reservation waits for replenishment or falls back to an alternative
warehouse, with no locks in the business logic.

Read the [inventory STM comparison source](https://github.com/adz/Axial/blob/main/examples/Axial.Comparisons/InventoryStm.fs).

The ordinary version is a lock, a counting semaphore for wakeups, and a hand-maintained invariant that stock and
reservation counts change together; the comment in the example marks exactly where swapping the semaphore for a
`Monitor` pulse introduces a missed wakeup.

{{< snippet id="compare-inventory" mode="no-check" reason="Excerpt from examples/Axial.Comparisons, whose tests compile and run it" >}}

The transaction either commits both `TRef` changes or neither; `STM.retry` suspends without blocking a thread and
re-runs from a coherent snapshot when any participating `TRef` changes; `orElse` chooses the alternative warehouse
inside the same transaction. The tests race two fibers for one unit per warehouse and assert nothing oversells, and
park a reservation on empty stock until a replenishment commits. ZIO correspondence: `TRef`, `STM.retry`, `orElse`.

- **Made visible by the type**: `STM<'value>` is a transaction value, separate from effects, composed before commit.
- **Enforced by the runtime**: all-or-nothing commit; coherent-snapshot retry.
- **Still the application's responsibility**: the guarantee covers STM-managed memory only; payment, database,
  HTTP, and logging effects stay outside the transaction. Axial's current STM serializes transactions through one
  lock; it favours correctness over throughput under high contention.

## Running the comparisons

```bash
dotnet test tests/Axial.Comparisons.Tests --nologo
```

Each [comparison source file](https://github.com/adz/Axial/tree/main/examples/Axial.Comparisons) defines the shared
domain types, then the `Ordinary` and `WithFlow` modules. Each implementation states its full return type.
