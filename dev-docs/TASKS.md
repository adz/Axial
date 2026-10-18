# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

Each task is one `- [ ] T<n>: <title>` line; details are indented under it. `grep '^- \[ \] ' dev-docs/TASKS.md`
lists the queue. Work on main.

## Queue

- [ ] T15b: Migrate gitkay and LogDug to Axial.Files and Axial.Elmish once 0.11 is published
  - Remove gitkay's walk/lines/watch Guardrails suppressions and swap LogDug's Files internals for the package.
  - Replace both apps' flow-to-Elmish bridges with the per-program runtime; replace gitkay's slots and debounce
    with keyed commands and an inbox subscription, and await cleanup on shutdown.
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
- [ ] T20b: Skip AXG001 for `.Result` guarded by `IsCompleted*`
  - The blocking-wait rule flags `if t.IsCompletedSuccessfully then t.Result`, which cannot block; the runtime marks
    these as boundaries (`src/Axial/Platform.fs` file marker and line markers in Core/Flow/Concurrency). Exempt a
    `.Result`/`GetAwaiter().GetResult()` on a value whose `IsCompleted`, `IsCompletedSuccessfully`, or `Status` is
    tested in an enclosing `if`, and on the argument of a `ContinueWith` continuation; then drop the line markers.
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
