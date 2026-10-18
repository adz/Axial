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
- [ ] T16: `Axial.Elmish` package and `App.start` off the caller's context
  - Ships in 0.11; record in `dev-docs/releases/0.11.0.md`. Spec: `dev-docs/current-ideas/axial-elmish.md`. Needs T1.
  - First fix the verified shutdown deadlock in core: `App.start` runs `Async.StartWithContinuations` on the caller's
    thread and settles through its `SynchronizationContext`, so stopping on a UI thread never returns. Start on the
    thread pool and add `AppHandle.StopAndWait(timeout)`, safe on any thread.
  - Then extract gitkay's `src/Axial.Elmish` into a per-program runtime: one root scope, delivery only through a
    host-supplied `Post`, `OnDefect` wired to the host crash path, a per-runtime `FiberRegistry`, `Cmd.OfFlow`
    (`either`/`perform`/`attempt`, plus `Latest` keyed latest-wins), `Sub.OfFlowStream`, and `Inbox`.
  - Done when: the spec's required tests pass, and gitkay and Logs Digger can delete their own bridges.
- [ ] T15b: Migrate gitkay and LogDug to Axial.Files once 0.11 is published
  - Remove gitkay's walk/lines/watch Guardrails suppressions and swap LogDug's Files internals for the package.
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
