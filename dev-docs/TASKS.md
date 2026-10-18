# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

Each task is one `- [ ] T<n>: <title>` line; details are indented under it. `grep '^- \[ \] ' dev-docs/TASKS.md`
lists the queue. Work on main.

## Queue

- [ ] T1b: Recover resource-release cost
  - T1 made releases flows with their own scope and the acquisition runtime. Per 1000 scope closes with one release,
    `ScopeCloseBenchmarks` went from 860 us / 1587 KB to about 1223 us / 2048 KB (about 0.37 us and 460 B per
    release; about 210 B is the release flow itself). Empty close and parallel composition are unchanged.
  - Recover the rest without weakening the semantics (uninterruptible, acquisition env and runtime, own scope):
    candidates are a scope created only when the release registers something, avoiding the ambient runtime switch
    when the release completes synchronously without reading it, and sharing one release runtime per scope close.
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
- [ ] T16: `Axial.Elmish` package and `App.start` off the caller's context
  - Ships in 0.11; record in `dev-docs/releases/0.11.0.md`. Spec: `dev-docs/current-ideas/axial-elmish.md`. Needs T1.
  - First fix the verified shutdown deadlock in core: `App.start` runs `Async.StartWithContinuations` on the caller's
    thread and settles through its `SynchronizationContext`, so stopping on a UI thread never returns. Start on the
    thread pool and add `AppHandle.StopAndWait(timeout)`, safe on any thread.
  - Then extract gitkay's `src/Axial.Elmish` into a per-program runtime: one root scope, delivery only through a
    host-supplied `Post`, `OnDefect` wired to the host crash path, a per-runtime `FiberRegistry`, `Cmd.OfFlow`
    (`either`/`perform`/`attempt`, plus `Latest` keyed latest-wins), `Sub.OfFlowStream`, and `Inbox`.
  - Done when: the spec's required tests pass, and gitkay and Logs Digger can delete their own bridges.
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
- [ ] T17: `Flow.recover` for typed-failure-only recovery that changes the error type
  - Ships in 0.11; record in `dev-docs/releases/0.11.0.md`.
  - `Flow.orElseWith` keeps `'error`; `Flow.fold` also catches defects and interruption. Add
    `Flow.recover : ('error -> Flow<'env, 'nextError, 'value>) -> Flow<'env, 'error, 'value> -> Flow<'env, 'nextError, 'value>`
    that handles `Cause.Fail` only and passes defects and interruption through. Tests for each cause; guide entry.
- [ ] T18: Bounded and self-referencing `Cache`
  - Ships in 0.11; record in `dev-docs/releases/0.11.0.md`.
  - `Cache.makeBounded capacity` (LRU eviction, optional expiry against the monotonic clock) and `Cache.makeRec`
    (lookup receives the cache itself, e.g. nested archives), or a documented pattern if `makeRec` adds nothing.
  - Say in the Cache docs that `Cache.make` requires `'env :> IHasClock`.
- [ ] T20: Guardrails output and new rules
  - Ships in 0.11; record in `dev-docs/releases/0.11.0.md`.
  - When the analyzer exits because of findings, suppress or reword MSBuild's `MSB3073 ... FSharp.Analyzers.Cli.dll
    ... exited with code -2`, which reads like a tool crash.
  - Flag `TimeZoneInfo.Local` (ambient machine state, like the clock) under the guardrail of the package that provides
    the replacement, or document why it is excluded.
  - Flag blocking waits (`Async.RunSynchronously`, `.Wait()`, `.Result`, `GetAwaiter().GetResult()`) outside flows
    in host/UI code paths, and `Flow.run`/`RunSynchronously` producing a scope-owned service (`Cache`, `Hub`, `Queue`)
    that dies when the run returns.
- [ ] T21: Troubleshooting note for the value restriction on parameterless generic flows
  - `let load : Flow<'env, Never, Settings> when 'env :> ...` does not compile; it needs
    `let load<'env when ...> : Flow<...>`. One entry in the troubleshooting-types guide.
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
