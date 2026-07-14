# Compelling Flow Examples

Status: IDEA — candidate scenarios for examples and comparative documentation.

## Purpose

Show where ZIO's `ZIO<R, E, A>` model and Axial's `Flow<'env, 'error, 'value>` improve a real program, rather than
showing effect syntax in isolation. Every finished example should contain:

1. an ordinary implementation using `Task`, exceptions, cancellation tokens, and manually passed services;
2. an Axial implementation, with a short ZIO correspondence where useful;
3. failure-path tests for both implementations; and
4. a precise comparison of guarantees, remaining runtime risks, and extra constraints.

Do not claim that Flow makes side effects pure or prevents all defects. The gain is that expected failure,
dependencies, cancellation, resource lifetime, and composition policy become visible and testable.

## 1. Checkout orchestration with compensation

**Scenario:** reserve stock, charge a card, create a shipment, and release the reservation if charging or shipment
creation fails. Give each operation a distinct typed error and map them into `CheckoutError` at the bind site.

**Without Flow:** implement `Task<CheckoutReceipt>` with injected service parameters, `try/with`, and a compensation
call in `finally` or the relevant catch branch. Include the common bug where a newly added failure path skips release,
and show how exceptions erase the declared error set.

**With Axial:** define `IInventory`, `IPayments`, and `IShipping` in the environment; have operations return
`Flow<_, InventoryError/PaymentError/ShippingError, _>`; compose them in `flow { }` using `BindError` where each source
error enters `CheckoutError`; use `Flow.acquireReleaseWith` when the reservation really has lexical ownership.
The ZIO version should use environment services, typed errors, and `acquireRelease`/`ensuring`.

**Difference to demonstrate:** the Flow signature states both required capabilities and expected failures; cleanup is
attached to resource lifetime and runs on success, typed failure, defect, or interruption. It does not make remote
compensation transactional: idempotency keys and reconciliation are still required.

## 2. Resilient HTTP call with a retry budget

**Scenario:** fetch an exchange rate. Retry only transient transport failures, use exponential delay, stop after three
attempts, and turn a two-second timeout into `RateError.TimedOut`. Never retry malformed successful responses or
programming defects.

**Without Flow:** implement a recursive `Task<Result<Rate, RateError>>` around `HttpClient`,
`CancellationTokenSource.CancelAfter`, `Task.Delay`, and exception classification. Show how cancellation, timeout,
transport exceptions, and `Result` errors create nested control flow and how an overly broad catch can retry defects.

**With Axial:** expose the call through `IHttp`; return `Flow<'env, RateError, Rate>`; apply
`Flow.Runtime.timeout`, then `Flow.Runtime.retry` with `RetryPolicy<RateError>` whose predicate accepts only transient
errors. Use an injected `IClock` if timestamps enter the decision. The ZIO analogue is `timeoutFail` plus a typed
`Schedule` and `retry`.

**Difference to demonstrate:** retry and timeout are policies over a cold workflow; typed failures can be selected
without catching defects, and cancellation reaches sleeps and the running request. Constraints become stricter:
errors need an explicit taxonomy, the operation must be safe to repeat, and the service adapter must not hide a clock,
randomness, environment lookup, or other effect.

## 3. Parallel dashboard fan-out

**Scenario:** load account, recent orders, and recommendations concurrently. Account and orders are mandatory;
recommendations may fall back to an empty list on their typed failure. Cancel unnecessary work when a mandatory branch
fails.

**Without Flow:** use `Task.WhenAll`, linked cancellation sources, exception inspection, and hand-written fallback
logic. Include tests for two simultaneous failures and for a slow sibling after another branch fails.

**With Axial:** recover only the recommendation branch with `Flow.catchAll`, combine independent mandatory branches
with `Flow.zipPar`, and map their errors into one page error before composition. If first-success semantics are wanted,
make that a separate `Flow.race` example rather than subtly changing the contract. The ZIO parallels are `zipPar`,
`collectAllPar`, and typed `catchAll`.

**Difference to demonstrate:** concurrency policy is visible at the composition point; `zipPar` interrupts the loser
on failure and combines causes if both branches settle unsuccessfully. Flow does not prove that arbitrary adapters
honour cancellation, and concurrent external writes still need their own consistency design.

## 4. Scoped database transaction or temporary workspace

**Scenario:** acquire a connection or create a temporary directory, perform several fallible steps, then release it
exactly once even if work fails, throws, times out, or is interrupted.

**Without Flow:** use `use`/`await using`, `try/finally`, and a cancellation token. Add a version in which resource
construction succeeds but setup fails before ownership is transferred, a frequent source of leaks.

**With Axial:** use `Flow.acquireReleaseWith` for lexical lifetime, or `Flow.acquireRelease`/`Layer.acquireRelease`
when the resource must live for the enclosing runtime or provided layer. Compose setup and use as typed flows and test
release against every `Exit` shape. ZIO correspondence: `Scope`, `ZIO.acquireRelease`, and scoped layers.

**Difference to demonstrate:** acquisition and finalization form one construct, and finalizer failure is preserved in
the cause rather than silently replacing the primary outcome. The programmer must still choose the correct scope;
Flow cannot make a non-idempotent or blocking finalizer safe.

## 5. Application wiring that cannot omit a capability

**Scenario:** a scheduled report needs a clock, filesystem, report store, and logger; production and tests supply
different implementations.

**Without Flow:** show constructor injection or a function with four service arguments, then contrast it with a
service-locator version where a missing registration fails only at runtime. Test code should reveal the fixture burden
and the temptation to read `DateTimeOffset.UtcNow` directly.

**With Axial:** express requirements in `'env` through `IHas<IClock>`, `IHas<IFileSystem>`, and application-specific
services; build implementations with `Layer` and provide them once at the application edge. Use fixed
`Clock.fromValue` and in-memory adapters in tests. ZIO correspondence: environment requirements and `ZLayer`.

**Difference to demonstrate:** narrow functions cannot run until their declared environment is supplied, test doubles
replace capabilities without changing business code, and Process/Http adapters cannot hide extra operational effects.
The type proves presence and shape, not correct configuration or behavioral quality of an implementation.

## 6. Producer/consumer pipeline with backpressure and interruption

**Scenario:** stream process output or imported records, transform them, persist them, and stop the producer promptly
when the consumer fails or the caller cancels.

**Without Flow:** combine `IAsyncEnumerable`, channels, background tasks, linked tokens, and manual observation of
producer exceptions. Include a broken version that returns after the consumer fails while its producer keeps running.

**With Axial:** build a cold `FlowStream`, transform it, and consume with `FlowStream.runForEachFlow`; where explicit
coordination is needed, fork the producer, retain its `Fiber`, and always `join` or `interrupt` it. Use
`Flow.Process.stream` for the process-output variant. The ZIO analogue uses `ZStream`, scoped fibers, and interruption.

**Difference to demonstrate:** stream failure and environment requirements remain typed through the pipeline, while
fiber handles make lifecycle operations explicit. Do not imply unlimited automatic backpressure or safe detached work:
document the chosen buffering policy and require ownership of every forked fiber.

## 7. Atomic inventory reservation under contention

**Scenario:** two checkouts race to reserve the last unit. A reservation waits when stock may be replenished, or chooses
an alternative warehouse without locks leaking into business logic.

**Without Flow:** implement shared mutable state with `lock`/`SemaphoreSlim`, condition signalling, retry loops, and
cancellation. Exercise missed wakeups, partial updates across two values, and lock release on exceptions.

**With Axial:** hold stock in transactional references, compose reads and writes in `STM`, use `STM.retry` to suspend
until observed state changes and `STM.orElse` for the alternative warehouse, then commit atomically inside Flow. ZIO
correspondence: ZIO STM with `TRef`, `STM.retry`, and `orElse`.

**Difference to demonstrate:** the transaction either commits all state changes or none, retry re-runs from a coherent
snapshot, and callers do not coordinate locks manually. The guarantee covers only STM-managed memory; payment,
database, HTTP, logging, and other irreversible effects must remain outside the transaction.

## Generation rules

- Keep each comparison runnable and small enough to understand in one sitting; prefer one failure-focused test over
  broad happy-path scaffolding.
- Use the same domain types and service interfaces in both versions so the comparison isolates the workflow model.
- Show the full return type above each implementation. Avoid hiding the ordinary version behind helper abstractions
  that quietly recreate an effect system.
- For every claimed guarantee, include a test that would fail if it were removed: finalizer runs, retry predicate is
  selective, loser is interrupted, missing environment fails to compose, or STM changes remain atomic.
- Separate expected typed failures from defects and interruption. Do not present `Cause.Die` as another domain error.
- End each example with three explicit lists: **made visible by the type**, **enforced by the runtime**, and **still the
  application's responsibility**.
- Describe ZIO and Axial as sharing a design family, not as feature-equivalent. Use the current Axial API as the source
  of truth and omit any ZIO feature for which Axial has no honest counterpart.
