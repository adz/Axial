# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

Each task is one `- [ ] T<n>: <title>` line; details are indented under it. `grep '^- \[ \] ' dev-docs/TASKS.md`
lists the queue. Work on main.

## Queue

- [ ] T31: 0.12 package reshape (owner decisions 2026-10-31)
  - Move the `Clock`/`Random`/`Guid`/`Log` operations and `TestLog`/`TestPlatform`/`TestClock`/`runTest` from `Axial.PlatformService`
    into core `Axial` (keep their namespaces for 0.12); delete the duplicate live services so one implementation remains (T29 item).
  - Delete `Axial.PlatformService` (not deprecated); environment variables become a source in `Axial.Configuration` (T32), and old
    env-var typed reads are deleted. DECIDED (owner, 2026-10-31).
  - Merge `Axial.Files` into `Axial.FileSystem`. Check what Files, Hosting, Hosting.Node, HttpClient and Process still need from the
    old package after the move, and drop references they no longer need.
  - Migration rows in `dev-docs/releases/0.12.0.md`. Do this before T33 so docs pages move once.
- [ ] T32: `Axial.Configuration`, culture and time zone (0.12; DECIDED, owner, 2026-10-31)
  - `Axial.Configuration` lives here and references Reified; core remains Reified-free. Sources produce Reified Data: environment,
    CLI args on every host, JSON via FileSystem/Reified, an IConfiguration adapter in Axial.Hosting, browser query string.
    `Data.mergeAll` gives later-wins precedence; Schema decoding accumulates keyed errors; required/optional strings cover simple cases.
    Default `Config.load` returns a plain value captured by consumers; fake with Data.
  - Reified 0.12 prerequisites: Data.merge/mergeAll/ofEnvironment, case-insensitive key parse option, nested CLI `--Db:Port`, and
    `Secret<'v>` in Refinements with redacted errors/ToString. Axial 0.12 requires Reified 0.12.
  - `Config.processArg : Secret<'v> -> ProcessArgument` bridges config secrets into `Process.secret` arguments (no Reified
    reference in `Axial.Process`).
  - Culture/TimeZone are sealed values outside the runtime context. `Culture.system`/`TimeZone.local` are explicit effects;
    TestPlatform supplies invariant/UTC under `runTest`; browser/Fable culture comes from navigator.language.
    Keep values in the app model or pass them to pure `Format.number/currency/date/time` and `TimeZone.toLocal` (UTC-to-local only).
    Culture implements IFormatProvider on .NET; Fable formatting uses Intl.
  - `Culture.setProcessDefault` is an effect called once at desktop/browser startup, not in App; Fable no-op. API servers use
    invariant globalization; ASP.NET automatically passes RequestLocalization's request culture as a value when enabled.
    Verify .NET WASM culture data in Avalonia browser builds. Guardrails implements the F# culture bans itself, retaining CA ids.
  - Optional `Config.watch sources schema` returns a SubscriptionRef of latest valid config: filesystem watches, FlowStream.debounce,
    re-read/merge/decode; env/args static, successful decodes published, last good retained, failures logged and exposed as an error stream.
  - Configuration docs present Axial.Configuration as Axial's approach and document Microsoft IConfiguration/Options via Axial.Hosting
    as a supported alternative, including the bridge. Design in `dev-docs/current-ideas/configuration.md`.
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

- [ ] T16: Reassess extraction of packages using Reified once their product boundary is settled
  - DECIDED (owner, 2026-10-31): core `Axial` has no Reified references; packages that need Reified data (the HTTP adapters,
    `Axial.Configuration`) may. They live in this repo for now and may be extracted later.

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
- DECIDED (owner, 2026-10-31): core `Axial` has no Reified references; packages that need Reified data (the HTTP adapters,
  `Axial.Configuration`) may. They live in this repo for now and may be extracted later.
- The reference app lives in [axial-examples](https://github.com/adz/axial-examples).
- Package, AOT, Fable, documentation, and site checks pass at release boundaries.
