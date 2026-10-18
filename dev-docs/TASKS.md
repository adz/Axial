# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

Each task is one `- [ ] T<n>: <title>` line; details are indented under it. `grep '^- \[ \] ' dev-docs/TASKS.md`
lists the queue. Work on main.

## Queue

- [ ] T1: Flow-valued resource release and `Layer.fromFlow`
  - Ships in 0.11; record breaking changes and additions in `dev-docs/releases/0.11.0.md`.
  - Release and layer construction are raw `Task`/`Async` callbacks, so cleanup cannot use `Ref`, `'env` services,
    or typed failures, and docs fall back to `lock`.
  - Release becomes `'value -> Flow<'env, 'none, unit>`: it runs uninterruptibly with the environment captured at
    acquisition, and a failure is a defect (as in ZIO). Change `Resource.create`, `Resource.ofAsync`,
    `Resource.finalizer`, `Resource.asyncFinalizer`, `Flow.scopeAcquireRelease`, `Flow.scopeFinalizer`, and
    `Flow.scopeAsyncFinalizer` (`src/Axial/Flow.fs`). Before 1.0, drop the task/async forms or keep them only as thin
    adapters.
  - Add `Layer.fromFlow`; make `Layer.acquireRelease` and `Layer.addFinalizer` take flow releases. `Layer.fromAsync`,
    `fromTask`, and `fromValueTask` (`src/Axial.Layers/Layer.fs`) hand the caller the raw `Scope` and expect a
    hand-built `Exit`; replace their uses in the layers guide, its tutorial, `Axial.PlatformService`, and the layers
    torture scenario.
  - Rewrite doc examples to use `Ref` instead of locks or `Interlocked`: `docs/09-scopes/_index.md` (connection log),
    `docs/08-concurrency-and-state/01-fibers.md` (lines ~163 and ~190), `04-deferred-semaphore.md`, `09-cache.md`,
    and `02-supervision.md` (poll counter).
  - Benchmark `ParallelCompositionBenchmarks` and scope close before and after; the empty-close fast path in
    `Platform.runFinalizers` must stay allocation-free.
- [ ] T2: Flow-shaped process I/O and web handlers
  - Ships in 0.11; record changes in `dev-docs/releases/0.11.0.md`.
  - `Input.readOnce`, `Input.produce`, `Output.callback`, and the `Sink` target (`InputSource`/`OutputTarget` in
    `src/Axial.Process/ProcessTypes.fs`, helpers in `ProcessDsl.fs`) take `Async` callbacks with no environment. Add a
    `FlowStream<_, _, byte[]>` input and a flow output callback (or output into a `Queue`).
  - Open question, ask before implementing: how a process spec carries `'env`.
  - `AspNetCore.handleParsed` (`'model -> Task<IResult>`) and `GenHttp.handleParsed` (`'model -> ValueTask<IResponse>`)
    are unused by docs and examples: take a flow or remove them.
- [ ] T3: Services return flows for asynchronous members
  - Ships in 0.11; record changes in `dev-docs/releases/0.11.0.md`.
  - Async members return `Flow<unit, 'error, 'value>`, with dependencies supplied at construction; `IProcess.Execute`
    already does this. Covers `IFileSystem` (`Task`) and `IHttp.Send` (`Async<Result<...>>`).
  - Keep `IClock` as it is (the runtime schedules with it) and keep synchronous members synchronous (`UtcNow`, `ILog`,
    `IRandom`, `IGuid`).
  - Update live implementations, the test doubles (`FileSystem.test`, `Http.test`), and guides; let recording fakes
    in the services and dependencies guides use `Ref`.
  - Open question, ask before implementing: the typed error for `IFileSystem` and `IHttp` failures.
- [ ] T4: `FlowStream.fromCallback` for .NET callbacks
  - Ships in 0.11; record changes in `dev-docs/releases/0.11.0.md`. Needs T1 (the subscription's release is a flow).
  - Bridge .NET callbacks (events, `IObservable`, `ActivityListener`, runtime hooks) into a stream fed through a
    scope-owned queue, with the subscription released when the stream ends, like ZIO's `ZStream.async`.
    `Queue.tryOffer` is already synchronous.
  - Use it to remove the locks in the telemetry guide (`ActivityListener`) and the supervision guide
    (`OnUnobservedDefect`, possibly as a defect stream).
  - Consider an `Axial.Guardrails` rule for `lock` inside `flow { }`.
- [ ] T5: Test Reified HTTP adapters against NuGet packages
  - `Reified.*` packages are on NuGet (latest 0.8.2; `Directory.Build.props` pins `ReifiedVersion` 0.7.0). Restore and
    test `Axial.Hosting.AspNetCore`, `Axial.Hosting.GenHttp`, their examples and tests, and
    `examples/Axial.ReferenceApp` against package references.
- [ ] T15: `Axial.Files` package: stream-native walk, lines, watch, durable writes
  - Ships in 0.11; record in `dev-docs/releases/0.11.0.md`. Needs T3 (flow-returning `IFileSystem`) and T4
    (`FlowStream.fromCallback` for watcher events). Scheduled ahead of T8.
  - `Axial.FileSystem` mirrors `System.IO` one call at a time; apps fall back to raw `System.IO` for anything
    composite (gitkay carries 19 Guardrails suppressions, mostly in `Git/Trash.fs` and `GitService.fs`). Package the
    generic part of LogDug's Files service (`~/projects/logdug/src/LogDug.Core/Files/Files.fs`): `walk` (lazy,
    descend predicate), `lines` (batched, binary-sniffing, encoding-aware reader as a `Resource`), and `watch` as a
    `FlowStream` with recursive watching (`IncludeSubdirectories`) and a scope-owned watcher. Leave LogDug's archive
    navigation and `Location` model in LogDug unless they generalise cleanly.
  - Built on `IFileSystem` so `FileSystem.test` covers it; `FileSystemWatcher` is the one new effect, behind its own
    mockable member and a guardrail rule in `EffectCatalog.fs`.
  - Durable writes live here too (not BCL API, so not on `IFileSystem`): atomic write (temp file in the same
    directory, `Flush(true)`, `MoveFile` with overwrite; directory fsync is not in the BCL, decide whether to add it as
    an explicit live dependency) and a bounded append-only log (checksummed records, torn-tail recovery, size cap with
    rotation). Process kills need only the rename; power loss needs the flushes.
  - Done when: gitkay's filesystem suppressions that come from walking, reading lines, or watching are gone; LogDug
    can swap its Files internals for the package; recursive watch has a live test; a crash test (kill a child process
    at random points while writing) never yields a lost, duplicated, or partially written record.
- [ ] T8: Process output cap that fails
  - Ships in 0.11; record changes in `dev-docs/releases/0.11.0.md`.
  - Kill on interrupt and stage/overall timeouts already exist (`ProcessLive.fs` `terminate`). The only failing limit
    today is `MaxLineLength` (`ProcessError.LineTooLong`), which is per line and applies only where output is framed
    into lines; `Chunks` framing and batch capture have no total limit, and `OutputRetention.Tail` truncates silently,
    which corrupts `--json` output. Add a total-bytes limit per channel, independent of framing, that terminates the
    process tree and fails with a typed `ProcessError` when exceeded.
  - Done when: a test runs a command that overflows the cap and gets the typed failure with no orphaned process.
- [ ] T10: Fiber-local context carried into logs
  - Ships in 0.11; record in `dev-docs/releases/0.11.0.md`. Replaces the `LATER_TODO.md` "fiber-local state" item.
  - Add FiberRef-style fiber-local values (make, get, set, locally), inherited by forked fibers. Have `ILog`
    implementations and `Hosting.observer` attach the current context (correlation ID, unit ID) to each entry.
  - Fork is a hot path: benchmark `ParallelCompositionBenchmarks` before and after; no cost when no fiber-local is set.
  - Done when: a test shows one job ID on every log line of a job, across forked fibers.
- [ ] T12: Wakeable reissue combinator for long polls
  - Ships in 0.11; record in `dev-docs/releases/0.11.0.md`.
  - A combinator that runs a flow and, when a wake signal fires, interrupts and reissues it; the wake is neither a
    failure nor a retry attempt for `Flow.retry`/schedules. Decide its module by its input (likely `Flow`).
  - Done when: a test shows wake-reissue cycles with zero counted failures and a long-poll guide example over `Http`.
- [ ] T13: `Axial.WebSocket` client service
  - Ships in 0.11; record in `dev-docs/releases/0.11.0.md`. Needs T3 (flow-returning services) and T4
    (`FlowStream.fromCallback` for the receive stream).
  - New package shaped like `Axial.HttpClient`: mockable `IWebSocket` service, a pluggable connect hook (the
    `SocketsHttpHandler.ConnectCallback` equivalent), TLS options as data (client certificate, trust-anchor-only
    validation, TLS 1.3 minimum), and a wire byte-counting hook. Add its guardrail (`new ClientWebSocket`) to
    `EffectCatalog.fs`.
  - Done when: a test opens a wss connection with mTLS against a local server and reports wire bytes; MQTT itself stays
    outside Axial (thin MQTTnet-backed service in the application).
- [ ] T14: OS service hosting with watchdog
  - Ships in 0.11; record in `dev-docs/releases/0.11.0.md`.
  - Windows service and systemd support in `Axial.Hosting` (`UseWindowsService`, `UseSystemd`). Microsoft's systemd
    package sends READY but not `WATCHDOG=1`: add a watchdog fiber that pings at half `WATCHDOG_USEC` only while the
    runtime makes progress. Document SCM recovery (`sc failure`) and a systemd unit with `Restart=` and `WatchdogSec=`.
  - Done when: `kill -9` restarts the agent under systemd, and a deliberately hung runtime is restarted by the
    watchdog (scripted test, Linux; Windows documented).
- [ ] T6: Move the reference app and host examples to their own repository
  - Move the cross-product reference application and host examples to a separate integration/examples repository
    after both release trains are public.
- [ ] T7: Reassess `LATER_TODO.md` Flow work against a real application
  - Reassess remaining demand-driven Flow work in `LATER_TODO.md` against a concrete application before expanding the
    API.

## Acceptance

- `Axial.slnx` builds and tests without Reified.
- Core `Axial` and operational packages contain no Reified references.
- Only the isolated HTTP adapters and retained reference application cross the product boundary.
- Package, AOT, Fable, documentation, and site checks pass at release boundaries.
