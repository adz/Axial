# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

Each task is one `- [ ] T<n>: <title>` line; details are indented under it. `grep '^- \[ \] ' dev-docs/TASKS.md`
lists the queue. Work on main.

## Queue

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
- [ ] T29: T25a review nice-to-haves
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
    `IHasClock`, typed-error failure predicate, defects configurable, interruption never counts, open error mapped
    into the caller's error type, consecutive-failure trip plus half-open probes, state changes on a `Hub`.
  - Rate limiter bridge to `System.Threading.RateLimiting` with a typed rejection (.NET only; decide Fable).
  - Hedging as a documented recipe over `Flow.race`; a "Coming from Polly" page mapping every Polly strategy.
  - Adaptive concurrency stays a `dev-docs/current-ideas/` sketch until an application needs it.

## Acceptance

- `Axial.slnx` builds and tests without Reified.
- Core `Axial` and operational packages contain no Reified references.
- Only the isolated HTTP adapters cross the product boundary here; the reference app lives in [axial-examples](https://github.com/adz/axial-examples).
- Package, AOT, Fable, documentation, and site checks pass at release boundaries.
