# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

Each task is one `- [ ] T<n>: <title>` line; details are indented under it. `grep '^- \[ \] ' dev-docs/TASKS.md`
lists the queue. Work on main.

## Queue

- [ ] T15b: Migrate gitkay and LogDug to Axial.Files and Axial.Elmish once 0.11 is published
  - Remove gitkay's walk/lines/watch Guardrails suppressions and swap LogDug's Files internals for the package.
  - Replace both apps' flow-to-Elmish bridges with the per-program runtime; replace gitkay's slots and debounce
    with keyed commands and an inbox subscription, and await cleanup on shutdown.
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
