---
weight: 10
title: Task vs Flow, Seven Scenarios
description: The same seven programs written with Task/exceptions/tokens and with Flow, with failure-path tests for every claimed guarantee.
---

# Task vs Flow, Seven Scenarios

Effect syntax in isolation proves nothing. This page walks through seven realistic programs implemented twice in
[`examples/Axial.Flow.Comparisons`](https://github.com/adz/Axial/tree/main/examples/Axial.Flow.Comparisons) —
once with `Task`, exceptions, cancellation tokens, and manually passed services, and once with
`Flow<'env, 'error, 'value>` — over identical domain types and service interfaces, so the only variable is the
workflow model.

Flow does not make side effects pure and does not prevent all defects. The gain, scenario by scenario, is that
expected failure, dependencies, cancellation, resource lifetime, and composition policy become **visible in the
signature** and **testable at the composition point**. Every guarantee claimed below has a test in
[`tests/Axial.Flow.Comparisons.Tests`](https://github.com/adz/Axial/tree/main/tests/Axial.Flow.Comparisons.Tests)
that fails if the guarantee is removed.

Each scenario ends with three lists: what the type makes visible, what the runtime enforces, and what remains the
application's responsibility. The design family is shared with ZIO; correspondences are noted where they help, and
omitted where Axial has no honest counterpart.

## 1. Checkout orchestration with compensation

Reserve stock, charge a card, create a shipment; release the reservation when charging or shipment creation fails.

The ordinary version is a `Task<CheckoutReceipt>` whose expected failures leave the signature as exceptions, and
whose compensation lives in a catch block someone must remember. The comparison includes `checkoutBuggy`, the common
real-world edit — a failure branch added outside the `try` — and a test proving the reservation leaks.

```fsharp
// Flow<CheckoutEnv, CheckoutError, CheckoutReceipt>
Flow.acquireReleaseWith
    reserve                                                  // acquire: typed InventoryError -> CheckoutError
    (fun (reservation, inventory) _ -> inventory.Release reservation)  // release: attached to the resource
    (fun (reservation, _) -> fulfil reservation)             // use: charge + ship, each error mapped at its bind
```

Failures enter `CheckoutError` at each bind site with `Bind.mapError`; the release action is attached to the
reservation's lexical lifetime, so it runs on success, typed failure, defect (a test throws from the shipping
adapter), and interruption. ZIO correspondence: environment services, typed errors, `acquireRelease`.

- **Made visible by the type**: the required services (`CheckoutEnv`) and the complete failure set (`CheckoutError`).
- **Enforced by the runtime**: release runs on every exit shape, including defects the catch-based version can miss.
- **Still the application's responsibility**: remote compensation is not transactional — idempotency keys and
  reconciliation are still required.

## 2. Resilient HTTP call with a retry budget

Fetch an exchange rate through [`Axial.Flow.HttpClient`]({{< relref "/flow/http/" >}}): retry only transient
transport failures, back off exponentially, stop after three attempts, and turn a two-second deadline into
`RateError.TimedOut`. Never retry malformed successful responses.

The ordinary version interleaves a retry loop, `CancellationTokenSource.CancelAfter`, `Task.Delay`, and exception
classification in one function — and one overly broad `with _ ->` away from retrying a `NullReferenceException`.

```fsharp
request                                          // Flow<'env, RateError, Rate>, cold
|> Flow.Runtime.retry transientOnly              // RetryPolicy<RateError>: ShouldRetry = Transport only
|> Flow.Runtime.timeout (TimeSpan.FromSeconds 2.0) TimedOut
```

Retry and timeout are policies applied to a cold workflow from outside. The retry predicate selects typed failures;
defects and interruption are structurally out of its reach — `Flow.Runtime.retry` re-runs `Cause.Fail` only. The
tests pin all four behaviors: recovery within the budget, budget exhaustion, no retry of `Malformed`, and the
timeout interrupting a hung request. ZIO correspondence: `timeoutFail`, typed `Schedule`, `retry`.

- **Made visible by the type**: the transient/terminal error taxonomy and the `IHas<IHttp>` requirement.
- **Enforced by the runtime**: only predicate-accepted typed failures are retried; the timeout reaches sleeps and
  the in-flight request through the ambient cancellation token.
- **Still the application's responsibility**: the operation must be safe to repeat, and the adapter must not hide a
  clock or other effect.

## 3. Parallel dashboard fan-out

Load account, orders, and recommendations concurrently. Account and orders are mandatory; recommendations fall back
to an empty list on their typed failure; a mandatory failure interrupts the still-running sibling.

```fsharp
let recommended = loadRecommendations |> Flow.orElse (Flow.succeed [])  // recover ONLY this branch

Flow.zipPar (Flow.zipPar account recent) recommended
|> Flow.map (fun ((account, recent), recommended) -> { ... })
|> Activity.trace "dashboard.load"                                       // Axial.Flow.Telemetry span
```

The ordinary `Task.WhenAll` version must cancel siblings by hand through a linked token source, and two simultaneous
failures surface as whichever exception `WhenAll` publishes first. With `zipPar` the interruption is the runtime's
job — the test's slow sibling awaits `Task.Delay` on its runtime token and asserts the resulting
`OperationCanceledException` was observed — and concurrent failures merge as `Cause.Both`. First-success semantics
are a different contract, so they appear as a separate `Flow.race` example rather than a subtle change to this one.
ZIO correspondence: `zipPar`, typed `catchAll` (here `orElse`), `race`.

- **Made visible by the type**: which branch may fail silently (none — the fallback is explicit at the composition).
- **Enforced by the runtime**: loser interruption and cause merging.
- **Still the application's responsibility**: Flow cannot prove an arbitrary adapter honours cancellation, and an
  adapter that registers its own extra observer on the runtime token — instead of catching the cancellation the
  operation it's already awaiting throws — can silently miss the interrupt; see
  [Task and Async Interop](../core-concepts/task-async-interop.md#pitfall-dont-register-a-second-cancellation-observer-on-the-same-token).

## 4. Scoped temporary workspace

Create a temporary directory through [`Axial.Flow.FileSystem`]({{< relref "/flow/filesystem/" >}}), perform fallible
steps, remove the directory exactly once on every exit shape.

The ordinary comparison includes `importBatchLeaky`, the classic leak: construction succeeds, then a setup check
throws *before* ownership transfers into `try/finally`. The test proves the directory survives. In the Flow version
there is no such gap — `Flow.acquireReleaseWith` owns the resource from the instant acquisition succeeds, and the
failing gate lives inside the resource's lifetime:

```fsharp
Flow.acquireReleaseWith
    acquire                                                     // FileSystem.createDirectory, typed errors
    (fun workspace _ -> Task.Run(fun () -> Directory.Delete(workspace, recursive = true)))
    (fun workspace -> flow {
        do! if List.isEmpty records then Flow.fail NoRecords else Flow.succeed ()
        do! FileSystem.writeAllLines (Path.Combine(workspace, "batch.csv")) records
            |> Flow.mapError (FileSystemError.describe >> UnreadableBatch)
        return records.Length
    })
```

ZIO correspondence: `Scope` and `ZIO.acquireRelease`; for resources that should live as long as a provided layer,
Axial has `Flow.acquireRelease` and `Layer.acquireRelease`.

- **Made visible by the type**: acquisition and finalization form one construct with one signature.
- **Enforced by the runtime**: the finalizer runs on success, typed failure, defect, and interruption, and a
  finalizer failure is preserved in the cause rather than replacing the primary outcome.
- **Still the application's responsibility**: choosing the correct scope; keeping finalizers idempotent.

## 5. Application wiring that cannot omit a capability

A daily report needs a clock, a filesystem, a console, and a report store. The ordinary version threads four
constructor parameters through every caller, and nothing stops a hurried edit from reading
`DateTimeOffset.UtcNow` directly.

The Flow version declares the capability set once as an environment record implementing `IHas<'service>` per
capability, and business code names only what it uses through the package operations
([`Clock.now`]({{< relref "/flow/platform-service/" >}}), [`FileSystem.readAllText`]({{< relref "/flow/filesystem/" >}}),
[`Console.writeLine`]({{< relref "/flow/console/" >}})):

```fsharp
let writeDailyReport (sourcePath: string) : Flow<ReportEnv, ReportError, string> =
    flow {
        let! now = Clock.now
        let! body = FileSystem.readAllText sourcePath |> Flow.mapError (FileSystemError.describe >> StoreRejected)
        ...
    }

// Production edge: live services merged into the record once.
Layer.merge (Layer.merge Clock.layer FileSystem.layer) Console.layer
|> Layer.map (fun ((clock, fileSystem), console) -> { Clock = clock; FileSystem = fileSystem; Console = console; Store = store })
```

The test builds the same record from `Clock.fromValue` and in-memory doubles and asserts a deterministic report
name — no time mocking framework, no service locator. Reading the clock without declaring it does not compile.
ZIO correspondence: environment requirements and `ZLayer`.

- **Made visible by the type**: the full capability set; a missing capability is a compile error at the edge.
- **Enforced by the runtime**: nothing at runtime needs to fail — the proof happened at compilation.
- **Still the application's responsibility**: the type proves presence and shape, not configuration quality.

## 6. Producer/consumer pipeline with backpressure and interruption

Stream records (or live process output), transform them, persist them, and stop the producer promptly when the
consumer fails.

The ordinary version combines a bounded `Channel`, a background producer task, a linked token source, and manual
observation of the producer's exception — and its classic bug (returning after a consumer failure while the
producer keeps writing) is one forgotten `linked.Cancel()` away.

A `FlowStream` is cold and pull-based: when persistence fails, the stream is simply never pulled again. The test
streams from an instrumented infinite sequence and asserts almost nothing was produced past the failing element.
The process variant uses [`Process.stream`]({{< relref "/flow/processes/" >}}) — typed `ProcessEvent`s from a live
process through the same pipeline shape:

```fsharp
Process.stream specification
|> FlowStream.mapError (ProcessError.describe >> BadRecord)
|> FlowStream.choose (function
    | ProcessEvent.Output output when output.Channel = OutputChannel.StdOut -> Some(output.Text.TrimEnd('\r', '\n'))
    | _ -> None)
|> ...  // same consumer as the in-memory variant
```

Where a producer must genuinely run ahead, `Flow.fork` returns a `Fiber` the caller owns and must `join` or
`interrupt`. ZIO correspondence: `ZStream`, scoped fibers, interruption.

- **Made visible by the type**: stream failure and environment requirements stay typed through the pipeline; a
  forked producer is a value someone must own.
- **Enforced by the runtime**: pull-based evaluation stops the producer with the consumer; fiber interruption
  reaches sleeps.
- **Still the application's responsibility**: the buffering policy (`Process.stream` delivery is bounded;
  document your own), and never detaching a fiber without an owner.

## 7. Atomic inventory reservation under contention

Two checkouts race for the last unit; a reservation waits for replenishment or falls back to an alternative
warehouse — without locks leaking into business logic.

The ordinary version is a lock, a counting semaphore for wakeups, and a hand-maintained invariant that stock and
reservation counts change together; the comment in the example marks exactly where swapping the semaphore for a
`Monitor` pulse introduces a missed wakeup.

```fsharp
STM.atomically (
    STM.orElse
        (reserveFrom inventory.LocalStock Local inventory)      // STM.retry when empty
        (reserveFrom inventory.RegionalStock Regional inventory))
```

The transaction either commits both `TRef` changes or neither; `STM.retry` suspends without blocking a thread and
re-runs from a coherent snapshot when any participating `TRef` changes; `orElse` chooses the alternative warehouse
inside the same transaction. The tests race two fibers for one unit per warehouse and assert nothing oversells, and
park a reservation on empty stock until a replenishment commits. ZIO correspondence: `TRef`, `STM.retry`, `orElse`.

- **Made visible by the type**: `STM<'value>` is a transaction value, separate from effects, composed before commit.
- **Enforced by the runtime**: all-or-nothing commit; coherent-snapshot retry.
- **Still the application's responsibility**: the guarantee covers STM-managed memory only — payment, database,
  HTTP, and logging effects stay outside the transaction. Axial's current STM serializes transactions through one
  lock; it favours correctness over throughput under high contention.

## Running the comparisons

```bash
dotnet test tests/Axial.Flow.Comparisons.Tests --nologo
```

Each source file in `examples/Axial.Flow.Comparisons` is self-contained: shared domain types at the top, the
`Ordinary` module, then the `WithFlow` module, with the full return type stated above each implementation.
