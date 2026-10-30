# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

Each task is one `- [ ] T<n>: <title>` line; details are indented under it. `grep '^- \[ \] ' dev-docs/TASKS.md`
lists the queue. Work on main.

## Queue

- [ ] T30: Throw safety: user code that throws is `Cause.Die` at every combinator (matrix in place; leaf wrapper next)
  - Invariant: an exception thrown by user code inside a flow (a mapper or handler, a user flow, a Task/ValueTask/Async factory,
    a sequence, a `Dispose`, a scope finalizer, a schedule step), synchronously or after an await, is observed as `Cause.Die`
    carrying that exception by every combinator: it never hangs, never skips compensation, never completes twice, never leaks a
    resource or a fiber. T28/T28b patched the known sites one by one; nothing enforces it centrally.
  - Matrix (done): `tests/Axial.Tests/ThrowSafetyMatrixTests.fs`, 273 cells x 2 modes (Sync: throws when called; AfterAwait:
    throws on a continuation after a real suspension). Each case runs within a 5 s bound and checks the outcome (Die with the
    original exception exactly once, or the documented value), compensation once, acquired = released, and that a started
    sibling (forked waiter, second memoize/Cache caller, joiner, back-pressured Hub/Queue producer) is released. A reflection
    fact fails when a public member of Flow/FlowStream/Layer/Cache/Hub/Queue/Dequeue/Schedule/Retry/Resource or a `Scope`
    method has neither a cell nor an exemption. `knownGaps` must equal the failing set, so a fix forces removing its entry.
    Reduced Fable copy with an explicit table: `tests/Axial.Fable.Tests/ThrowSafetyMatrixTests.fs`. Gap list and analysis:
    `~/projects/scratch/axial-coord/pi/t30-matrix.report.md`.
  - Next, the leaf wrapper: convert a throw at each user-code call site (binder/mapper invocation in bind/map/delay and the
    trampoline continuation after an await, builder continuations, interop factories) into `Execution.ofCause (causeOfThrown
    ct e)`, so no combinator is handed a thrown or faulted execution. It must close the matrix's throw-specific gaps (parallel
    siblings leaked after an await; `buffer`/`mergePar` hang through `feed`) with the sync path allocation-free (FiberCost,
    PlatformRead, FoldCause, a bind/map loop). Then fold the per-site T28 copies it makes redundant (T29 N3).
  - Combinator gaps the wrapper does not fix (they fail with `Flow.die` too): `Layer.zipPar` waits for both branches and never
    interrupts the other one (hang beside a long-running branch; decide: interrupt, or document and change the cell);
    `timeoutToOk` drops a defect raised by the timed-out flow's `onInterrupt` cleanup instead of combining it, as `race` does.

- [ ] T31: 0.12 package reshape (owner decisions 2026-10-31)
  - Move the `Clock`/`Random`/`Guid`/`Log` operations and `TestLog`/`TestPlatform`/`TestClock`/`runTest` from `Axial.PlatformService`
    into core `Axial` (keep their namespaces for 0.12); delete the duplicate live services so one implementation remains (T29 item).
  - Rename what is left (environment variables) into `Axial.Configuration` (T32); deprecate the `Axial.PlatformService` NuGet id.
  - Merge `Axial.Files` into `Axial.FileSystem`. Check what Files, Hosting, Hosting.Node, HttpClient and Process still need from the
    old package after the move, and drop references they no longer need.
  - Migration rows in `dev-docs/releases/0.12.0.md`. Do this before T33 so docs pages move once.
- [ ] T32: `Axial.Configuration` and time zone/culture in the platform services (0.12; design first, owner review before code)
  - Configuration: typed reads with validation errors; sources: environment variables, command-line arguments (all hosts, not only
    Node), config files, an `IConfiguration` adapter; explicit precedence. Authority stays in the env (`IHasConfiguration`-style).
  - Time zone and culture join Clock/Random/Guid in the runtime's `PlatformServices`, with `Flow.withTimeZone`/`withCulture` and fixed
    values under `runTest`; no reads of `TimeZoneInfo.Local`/`CultureInfo.CurrentCulture` in Axial code paths.
  - Design in `dev-docs/current-ideas/configuration.md`.
- [ ] T33: A docs set per non-core package, shaped like `docs/process` and `docs/http`
  - Folder, `_index.md`, numbered pages, `api/`, an entry in `docs/packages/index.md`: FileSystem (with Files), Console,
    Configuration, WebSocket, Hosting (one set, a page per host package), Elmish, Telemetry (one set), Guardrails (with `Axial.Tool`).
  - Core chapters keep only core and link to the sets. After T31/T32.
- [ ] T23: Property-based concurrency law suite
  - Generate random flows over fork/race/zipPar/scope/interrupt and check the laws: a flow that starts work waits for it,
    cleanup runs once and after cancellation, a loser settles before `race` returns, defects are reported exactly once,
    interruption is never swallowed by `fold`/`recover`. Extend `examples/Axial.TortureTest` seeds, not a new harness.
  - Every law that fails becomes a fix with a pinning test. Benchmarks if a runtime path changes.
- [ ] T24: Fiber dumps show the root, what each fiber waits on, and attach late
  - Design: `dev-docs/current-ideas/fiber-dump-root-waits-attach.md`. Benchmarks required (FiberCost, Race, Pingpong/Threadring,
    a tiny `runSync` loop); allocation identical, time within noise. The torture watchdog shows waits in its stall dump.
- [ ] T25a: Platform in the runtime (Clock, Guid, Random), log sink slot, remove `ISecureRandom` (breaking)
  - Spec: `~/projects/scratch/axial-coord/pi/capabilities-spec.md`, sections 1, 2, 9, 10. Write
    `dev-docs/current-ideas/capabilities.md` (T25a part) first and stop for the owner's review before any code.
  - Includes `Flow.runTest` and `withClock`/`withRandom`/`withGuid`, `withLogSink`/`withTelemetry` slots (Console default, Info,
    stderr), explicit-stepping test clock replacing `runOnManualTime`, and the invariant tests (written first, failing).
  - Design approved in `capabilities.md` (decisions D1-D9). `ISecureRandom` is removed first, on its own, before 0.12. The rest is
    breaking: 0.12 only if it finishes with benchmarks, else 0.13. Benchmark runtime-context reads against env reads on
    sleep/timeout loops. Decide `platform-env-class.md` afterwards.
  - Status: implementation is complete locally (phases A-F and the R3 review fixes) and awaits the final verify and push. The
    0.12 breaking table is rebuilt against v0.11.3. The R3 follow-ups are tracked in T29 (T28 is fixed).
- [ ] T25b: Capability declarations (`ICapability`: Live/Fake/Replaces) and the build-time manifest
  - Spec sections 3, 4, 10. After T25a ships. Manifest in `obj/axial-capabilities.json` and the nupkg, `axial capabilities`,
    docs and `llms.txt`, agent skill; Guardrails effect rules built from merged manifests (28 built-in rules shrink).
- [ ] T25c: Guardrails rules for capabilities (naming, no generic IHas chains, no generic ICapability calls, Replaces resolve and
  are unique, Live only in host role)
  - Spec section 5. After T25b.
- [ ] T29: T25a review nice-to-haves (0.12: the renames, the Elmish `LogSink` option, the WebSocket `Connect` hook; rest can follow)
  - Elmish: add `ElmishRuntimeOptions.LogSink: ILogSink option`, installed where `Clock` is (`Pump()`, `startLayer`); today Fable's
    default sink sends `Log.info` to `console.error` (red) and the pump is internal. Do it before 0.12 so it is one break.
  - WebSocket `Connect` hook (`WebSocket.fs` ~230) runs as a fresh root and ignores the caller's `withClock`/`withLogSink`; capture
    `RuntimePlatform.current ()` and invoke it under that runtime as Process callbacks do, or document the defaults.
  - Duplicate live platform services (C1 #12): `PlatformDefaults` in `Core.fs` against `Clock.live`/`Random.live`/`Guid.live`/`Log.live`
    in `Axial.PlatformService`; keep one implementation (the two Fable sleep copies already differ on a null token).
  - Alias and naming cleanup before 0.12: `Log.none`/`Log.fromSink` against `LogSink.none`/`fromFunction`; `Clock.fixed`/`Clock.fromValue`;
    `Guid.sequence`/`fromSequence`; `TestLog.Log`/`LogException` (bypass annotations and locals); `TestPlatform.trace` and the public mutable
    `Reads` (use `Trace` and a snapshot).
  - `withCallerServices` (`ProcessLive.fs` ~244) copies six fields when only `Platform` differs: use `{ current with Platform = caller.Platform }`.
  - `LogSink.console` writes message and exception as two writes; write one string. Add a Fable test with two concurrent branches with
    different overrides. Cache the per-request logger and sink in ASP.NET `flowEndpoint`.
  - T28 review nice-to-haves (`pi/t28.review.md` N1-N3, N6): a real completed fast path in `Platform.mapBoth`/`mapBothObserved`
    (benchmark MapBothSuccessX1000); a sync fast path in builder `TryFinally` plus a `use`-in-loop row in FoldCauseBenchmarks; fold
    the five "capture sync throw, then observe the fault" copies into one helper (`Platform.isCompletedSuccessfully` to drop
    FlowBuilder's `#if`); `mapFlowParUsing` gives back its pooled resource when the mapper throws (use `foldObserved`).
- [ ] T27: Research richer effects: algebraic-effects style in F# (algeff) and language-ext v5 `Eff<RT,A>` runtime traits
  - Read both from source, write `dev-docs/current-ideas/richer-effects.md` comparing them with Platform/capabilities as
    designed in T25. No implementation. Preliminary draft written; T25a is implemented locally, so re-check its section 5 against
    what actually shipped (`capabilities.md` and the code), then remove this task.
- [ ] T26: Spike: absorb FIO's continuation runtime (MIT) behind `Flow`-as-data, torture it with T23
  - Needs T23 first. Copy only the interpreter and scheduler, with FIO's licence in THIRD-PARTY-NOTICES, a source header naming
    the upstream commit, and bugs sent upstream. Design: `dev-docs/current-ideas/flow-as-data.md`.
  - Accept only if every T23 law passes on both runtimes, Pingpong/Threadring improve, and nothing else regresses beyond
    noise. Fable and NativeAOT must work or it stays a .NET-only switch.

- [ ] T16: Split the HTTP host adapters into their own Axial+Reified product before 1.0
  - Move the two cross-product packages out of the Axial release train once their product boundary is settled.

- [ ] T15b: Migrate gitkay and LogDug to Axial.Files and Axial.ElmishFlows once 0.11 is published
  - Remove gitkay's walk/lines/watch Guardrails suppressions and swap LogDug's Files internals for the package.
  - Replace both apps' flow-to-Elmish bridges with the per-program runtime; replace gitkay's slots and debounce
    with keyed commands and an inbox subscription, and await cleanup on shutdown.
- [ ] T7: Reassess `LATER_TODO.md` Flow work against real applications
  - The remaining reference applications are work applications, reassessed from a separate (work-account) checkout.
    Brief: `dev-docs/handoffs/T7-reference-app-reassessment.md`; findings come back as
    `dev-docs/reassessment/<app>.md` on `chore/t7-reassessment-<app>` branches or patches.
  - Then merge the findings into `LATER_TODO.md` (promote, defer, or delete items) and close this task.
- [ ] T22: Resilience policies for Polly parity (shared, stateful policies beside the stateless `Schedule`)
  - Bulkhead: a bounded wait queue on `Semaphore` that rejects with a typed error when full.
  - Circuit breaker: `CircuitBreaker.make` and `CircuitBreaker.protect`; shared `Ref`/STM state, open duration on
    the runtime clock, typed-error failure predicate, defects configurable, interruption never counts, open error mapped
    into the caller's error type, consecutive-failure trip plus half-open probes, state changes on a `Hub`.
  - Rate limiter bridge to `System.Threading.RateLimiting` with a typed rejection (.NET only; decide Fable).
  - Hedging as a documented recipe over `Flow.race`; a "Coming from Polly" page mapping every Polly strategy.
  - Adaptive concurrency stays a `dev-docs/current-ideas/` sketch until an application needs it.

- [ ] T34: `Axial.Sql`: provider-agnostic ADO.NET access (after 0.12)
  - Connection as a layer, transaction as a scope (commit on success, roll back on failure or interruption), transient-error retry.
- [ ] T35: Browser storage capability for Fable apps (after 0.12)
  - `localStorage`/`sessionStorage`/IndexedDB behind a capability with a fake.

## Acceptance

- `Axial.slnx` builds and tests without Reified.
- Core `Axial` and operational packages contain no Reified references.
- Only the isolated HTTP adapters cross the product boundary here; the reference app lives in [axial-examples](https://github.com/adz/axial-examples).
- Package, AOT, Fable, documentation, and site checks pass at release boundaries.
