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
- [ ] T40: Clear the repository's test warnings (594 AXG001 in the 214ff4e90 verify build)
  - Rewrite blocking waits and delays in tests as flows run with `Flow.runTest` on the `TestPlatform` fakes, not tasks.
  - A test file that checks the real thing (a live server, the real clock) claims that capability on its `Compile`
    item instead. Referencing a package is not testing it.
- [ ] T26: Spike: absorb FIO's continuation runtime (MIT) behind `Flow`-as-data, torture it with T23
  - Needs T23 first. Copy only the interpreter and scheduler, with FIO's licence in THIRD-PARTY-NOTICES, a source header naming
    the upstream commit, and bugs sent upstream. Design: `dev-docs/current-ideas/flow-as-data.md`.
  - Accept only if every T23 law passes on both runtimes, Pingpong/Threadring improve, and nothing else regresses beyond
    noise. Fable and NativeAOT must work or it stays a .NET-only switch.

- [ ] T16: Reassess extraction of packages using Reified once their product boundary is settled
  - DECIDED (owner, 2026-10-31): core `Axial` has no Reified references; packages that need Reified data (the HTTP adapters,
    `Axial.Configuration`) may. They live in this repo for now and may be extracted later.

- [ ] T15b: Publish gitkay on Axial 0.17.0
  - LogDug shipped on Axial 0.16.0 as 0.5.2. gitkay is on 0.16.0 locally (main ahead of origin) and moves to 0.17.0
    once it is on NuGet: `Parallelism.count` replaces `Processors.fs` and its processors claim, and `UnobservedDefect.At`
    replaces the clock allowance in `CmdDiagnostics.fs`. Then Full build, tests, a human runs `axial policy approve`,
    push, green CI, tag v0.17.2.

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

- [ ] T42: A process whose descendant keeps its output pipes open must not hang the run
  - Seen in FsLiveDocs 0.14.6: `dotnet build`/`dotnet msbuild` left a reusable MSBuild node (and possibly the build
    server) holding the inherited stdout/stderr, so the run drained to EOF until the node shut down and CI hit the
    job timeout. The default drain-to-EOF behaviour waits on any descendant that holds the pipes, after any exit.
  - Decide the fix: a default bound on draining once every direct stage has exited (reporting the descendant), or
    keep drain-to-EOF and document node reuse and build servers with `-nodeReuse:false` and `drainTimeout` as remedies.
    Pin it with a test whose child leaves a background process holding stdout.
- [ ] T41: A failing pipeline stage stops the others without losing output already written
  - A stage exiting outside its success codes stops the stages still running, and the stop abandons every pipe, so
    output the exited stage or a downstream stage wrote but Axial had not read is lost. Stop only the live stages and
    keep reading each pipe to EOF (killed stages close theirs); pin it with a background-writer test like
    `a failing exit keeps all the output the process wrote`.
- [ ] T39: Add native Windows CloseMainWindow/CloseStdIn escalation coverage, including repeated Close acceptance during grace and timeout-owned shutdown.
- [ ] T38: Harden `HttpServiceTests.live service posts JSON bodies and reads status errors`
  - Failed once under full-suite load (2026-11-02 verify at 66f1d42c5), passes 15/15 alone; make its live-server timing deterministic.
  - 79ce86f01 gave the loopback server a free port; the server still serves one request at a time on a `Task.Run` loop.
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
