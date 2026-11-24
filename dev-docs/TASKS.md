# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

Each task is one `- [ ] T<n>: <title>` line; details are indented under it. `grep '^- \[ \] ' dev-docs/TASKS.md`
lists the queue. Work on main.

## Queue

- [x] T32: `Axial.Configuration`, culture and time zone (0.12; DECIDED, owner, 2026-10-31)
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
- [ ] T23: Property-based concurrency law suite
  - Generate random flows over fork/race/zipPar/scope/interrupt and check the laws: a flow that starts work waits for it,
    cleanup runs once and after cancellation, a loser settles before `race` returns, defects are reported exactly once,
    interruption is never swallowed by `fold`/`recover`. Extend `examples/Axial.TortureTest` seeds, not a new harness.
  - Every law that fails becomes a fix with a pinning test. Benchmarks if a runtime path changes.
- [ ] T24: Fiber dumps show the root, what each fiber waits on, and attach late
  - Design: `dev-docs/current-ideas/fiber-dump-root-waits-attach.md`. Benchmarks required (FiberCost, Race, Pingpong/Threadring,
    a tiny `runSync` loop); allocation identical, time within noise. The torture watchdog shows waits in its stall dump.
- [ ] T40: Clear the repository's test warnings (548 AXG001 in a Full build)
  - Rewrite blocking waits and delays in tests as flows run with `Flow.runTest` on the `TestPlatform` fakes, not tasks.
  - A test file that checks the real thing (a live server, the real clock) claims that capability on its `Compile`
    item instead. Referencing a package is not testing it.
- [ ] T27: Research richer effects: algebraic-effects style in F# (algeff) and language-ext v5 `Eff<RT,A>` runtime traits
  - Read both from source, write `dev-docs/current-ideas/richer-effects.md` comparing them with Platform/capabilities. No
    implementation. Preliminary draft written; re-check its section 5 against what shipped (`docs/guardrails/03-rule-authoring.md`,
    the platform services docs and the code), then remove this task.
- [ ] T26: Spike: absorb FIO's continuation runtime (MIT) behind `Flow`-as-data, torture it with T23
  - Needs T23 first. Copy only the interpreter and scheduler, with FIO's licence in THIRD-PARTY-NOTICES, a source header naming
    the upstream commit, and bugs sent upstream. Design: `dev-docs/current-ideas/flow-as-data.md`.
  - Accept only if every T23 law passes on both runtimes, Pingpong/Threadring improve, and nothing else regresses beyond
    noise. Fable and NativeAOT must work or it stays a .NET-only switch.

- [ ] T16: Reassess extraction of packages using Reified once their product boundary is settled
  - DECIDED (owner, 2026-10-31): core `Axial` has no Reified references; packages that need Reified data (the HTTP adapters,
    `Axial.Configuration`) may. They live in this repo for now and may be extracted later.

- [ ] T15b: Publish the gitkay and LogDug upgrades once Axial 0.16.0 is on NuGet
  - Both are upgraded and committed locally against `0.16.0-local.2` (gitkay c8b70ad, d1fb0b6, c7d0e65, 9d5e17e and
    87d62a4; LogDug f338eef, e159934 and c16dd6d; both on the .NET 11.0.100 SDK), with every other package at its
    latest. local.2 has the rule names, the concurrency claim and the blocking-wait rules: Full builds pass with
    findings only in test files, the tests pass (gitkay 450, LogDug 58) and both AOT self-tests pass. Switch
    `0.16.0-local.2` to `0.16.0` (package references, the `axial.tool` manifest and LogDug's untracked NuGet.config),
    restore from nuget.org, rebuild in Full mode, run the tests, then a human runs `axial policy approve` in each
    repository before pushing.
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

- [ ] T39: Add native Windows CloseMainWindow/CloseStdIn escalation coverage, including repeated Close acceptance during grace and timeout-owned shutdown.
- [ ] T38: Harden `HttpServiceTests.live service posts JSON bodies and reads status errors`
  - Failed once under full-suite load (2026-11-02 verify at 66f1d42c5), passes 15/15 alone; make its live-server timing deterministic.
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
