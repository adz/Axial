# Guardrails primitives handover — Phase 3

## Scope and state

Worktree: `/home/adam/projects/Axial/feature/flow-uninterruptible`, branch `feature/flow-uninterruptible`.
Commit only, no push. Original baseline: origin/main `f76d8476ccf800a00710a34422fde79e0a10ce43`.

Read the final Coordinator review of phase 2, then OWNER DIRECTION in
`/home/adam/projects/scratch/axial-coord/pi/guardrails-primitives.md`.
They supersede the original expanded primitive requirements. No delegated agents were run.

Persistent report (inventories, allocation tables, lifetime follow-ups, final validation outcomes):
`/home/adam/projects/scratch/axial-coord/pi/guardrails-primitives.report.md`.
The report and ignored artifacts record verification after this tracked handover's commit; audit
`artifacts/verify/result.json` against HEAD rather than inferring validation from this snapshot.

## Final split

Internal `Axial.Platform.Cancellation` has two meanings, neither public:

```fsharp
let never: CancellationToken = CancellationToken.None
let uninterruptible (cleanup: CancellationToken -> 'value) : 'value = cleanup never
```

`never` is the sole literal under `src/`. Roots, omitted public API tokens, scheduling tokens,
release runtime token fields, and the five reviewed lifetime sites use this value.
`uninterruptible` invokes only cleanup: release, scope close, onExit, race-loser settlement,
Stop requests and Dispose. No identity-function token extraction remains in source.
This is token substitution, not dynamic masking; existing release BranchToken/OuterToken masking,
ambient restoration, failure composition and scope ownership remain unchanged.

The report's Follow-up section assesses graceful-fiber parentage, shared publishers, replacement
handoffs, the process coordinator and Deferred.toTask. None of their lifetime semantics changes here.

No public Flow primitive, safe-reporting helper, Ref.Sync, Counter or interop helper was added.
Observer/sink catches, Random locks, adapters, analyzer/config/baseline and scripts/verify.sh remain
unchanged. The report inventories all adapter locks/Interlocked/TCS sites and conversion costs.

## Torture coverage

`examples/Axial.TortureTest/Scopes.fs` now gates cleanup for three paths on every round:
ensuring, scoped acquire/release, and a race loser's resource release. The owner is interrupted only
after cleanup announces entry. It must still be pending before the resume gate opens; cleanup must
finish exactly once and preserve the original typed failure or race winner success.
Throwing OnStart/OnEnd observers surround these owners, with invocation counts checked after settlement.
Gates use Deferred, cleanup counts use Ref, and gate/join waits have ten-second failure bounds.
The scenario runs in both .NET and Fable; no waiver is used.

## Earlier proof and measurements

- Phase 1 inventory: `0e49db564`.
- Tests first: `e960fd166`, `9673ecf68`; Phase 2 implementation: `cdd53df01`.
- Five new cases in `tests/Axial.Tests/UninterruptibleCleanupTests.fs`: helper token/result/exception
  identity; ensuring and scoped release cancellation; race-loser cancellation; async Scope disposal.
- Original-source compile-red is explicit: the internal primitive did not exist. It does NOT prove
  an original public behavioral bug; this is behavior-preserving centralization.
- Phase 2 canceled-token mutant: all five tests failed (`guardrails-tests-mutant.log`), restored before green.
- Phase 2 focused checks: 151 core + 16 App/Execution + 79 Process = 246 passed, none failed.
- FiberCost and ParallelComposition: all 20 benchmark cases ran before/after; no measured B/op increases.
  Full per-case allocations/timings and raw artifact paths are in the report. ForkResource time rose ~18%;
  no timing improvement is claimed.

## Final verification audit

All logs below live under `artifacts/verify/` unless noted:

- `guardrails-phase3-scopes.log`: targeted scopes torture soak.
- `fail-first.txt`: final-source proof via `scripts/check-tests-fail-without-fix.sh origin/main
  tests/Axial.Tests 'FullyQualifiedName~UninterruptibleCleanupTests'`; compile-red and behavioral
  mutant evidence remain distinct.
- `guardrails-phase3-grep.txt`: exactly one raw inert-token literal under src, and zero identity-runner uses.
- `result.json`: authoritative final clean-HEAD full build/test/Fable/AOT/docs/livedocs result.
- Detached full verify log: `/home/adam/projects/scratch/axial-coord/pi/guardrails-primitives-verify.log`.
- `guardrails-done-check.txt`: coordinator's mechanical done-check; only claim completion after PASS.

Use `setsid -f axial-verify`, never nohup; poll at 300 seconds. It holds the shared dotnet lock across
all stages. Timing-sensitive failures or exit 124 receive one unchanged full rerun before diagnosis.
Any later tracked commit requires full verification again. Do not push.
