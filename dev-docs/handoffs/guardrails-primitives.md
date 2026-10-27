# Guardrails primitives handover — Phase 2

## State

Phase 2 complete; STOP before Phase 3. Commit only, no push.

- Worktree: `/home/adam/projects/Axial/feature/flow-uninterruptible`, branch `feature/flow-uninterruptible`.
- Original source baseline: origin/main `f76d8476ccf800a00710a34422fde79e0a10ce43`.
- Phase 1 inventory commit: `0e49db564`.
- Tests-first commits: `e960fd166`, `9673ecf68`; implementation and final test-authoring corrections follow these commits.
- Brief: `/home/adam/projects/scratch/axial-coord/pi/guardrails-primitives.md`. Its final OWNER DIRECTION overrides earlier requirements and Phase 1 proposals.
- Full report (including complete inventories and before/after per-case allocations): `/home/adam/projects/scratch/axial-coord/pi/guardrails-primitives.report.md`.
- Dotnet allowed again. Shared dotnet wrapper serializes machine activity; lock queueing can take several minutes. Run long operations detached rather than allowing a tool timeout to kill them.
- No forbidden files changed: guardrail TOML/lock/baseline, scripts/verify.sh, analyzers. No existing tests edited.

Read TOKEN-RULES.md beside the brief and the full design `/home/adam/projects/Axial/main/dev-docs/current-ideas/guardrails-exceptionless.md` before resuming. No delegated agents were run.

## Implementation

Only new runtime primitive, internal to existing `Axial.Platform`:

```fsharp
module Cancellation =
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    let uninterruptible (cleanup: CancellationToken -> 'value) : 'value =
        cleanup CancellationToken.None
```

`src/Axial/Platform.fs:49` is now the ONLY literal occurrence under src (including XML). All 30 old code uses are replaced; three XML references now say non-cancelable token.

Cleanup sites pass the existing invoke/close operation. Root, scheduling and runtime-token fields use `Platform.Cancellation.uninterruptible id`; no second token primitive. This is token substitution, not dynamic masking of arbitrary flows. Existing release runtime BranchToken/OuterToken masking, ambient restoration, scope ownership, and failure composition remain intact.

F# `inline` failed at the Process friend/reference-assembly boundary (FS1116/FS1118); ordinary function plus JIT inlining attribute compiled and benchmarks show no allocation increases. No new public API or release note. Changed source files: Axial/{Platform,Flow,Core,Concurrency,App}.fs and Axial.Process/ProcessRun.fs.

Owner supersession applied:
- No public Flow.uninterruptible, reporting helper, Ref.Sync, Counter, or new interop function.
- Observer/sink catches unchanged (including direct telemetry sink's existing propagation behavior).
- Optional Random.Shared deferred; all five random locks remain unchanged.
- Adapter inventory ONLY: 36 locks, 12 Interlocked operations, 6 TCS declarations, 0 Monitor operations across 13 files; unchanged before/after. Report lists EVERY line, callback→Queue→owner feasibility, synchronous-boundary constraints, and estimated conversion costs. No adapter conversions performed.

## Tests and proof

New `tests/Axial.Tests/UninterruptibleCleanupTests.fs` (five cases):
- Internal runner uses inert token, invokes once, preserves return and thrown exception identity.
- Ensuring and scoped acquire/release: suspend cleanup, cancel caller, prove owner waits, resume cleanup, preserve original failure.
- Race loser: suspend release, cancel caller, prove race waits, resume, preserve winner success.
- Explicit async Scope disposal: suspend finalizer, cancel external caller, prove disposal waits and scope empties.

All gates have bounded 10-second waits; no timing sleeps or shared module-level fixtures. New tests use Flow.scopeAcquireRelease and cold Flow.fromTask bridges, not Resource use! or hot Task binds.

Artifacts under `artifacts/verify/`:
- `guardrails-tests-red.log`: new tests fail against original src, FS0039 missing internal Cancellation module. This is compile-red, NOT claimed as original behavioral failure: original public cleanup already behaved correctly.
- `guardrails-tests-mutant.log`: deliberately replaced helper's sole token with `CancellationToken(true)`; **all 5 failed**. Direct runner assertion fails behaviorally; other cases can fail their bounded entry gates because roots/cleanup see a canceled token. Mutation fully restored before green validation/benchmarks.
- `guardrails-focused.log`: **151 passed / 0 failed**, covering new tests, resources, structured ownership, concurrency, scopes, observers, diagnostics, runtime semantics.
- `guardrails-app-execution.log`: **16 passed / 0 failed**.
- `guardrails-process.log`: **79 passed / 0 failed**, ProcessServiceTests in Axial.Integration.Tests.
- Total distinct focused cases: **246 passed / 0 failed**. No unchanged timing-sensitive failure/rerun.

## Benchmarks

Both existing classes only, 20/20 cases each run, no concurrent dotnet jobs during measurement:

```bash
dotnet run -c Release --no-build --no-restore --project benchmarks/Axial.Benchmarks -- \
  --filter '*FiberCostBenchmarks*' '*ParallelCompositionBenchmarks*' \
  --artifacts artifacts/benchmarks/guardrails-before # then guardrails-after
```

Before runtime source identical to origin/main; after is implemented source. Existing definitions unchanged. SDK 10.0.402, .NET 10.0.13, BDN 0.15.8. Logs and CSV/Markdown reports under `artifacts/benchmarks/guardrails-{before,after}/`; both exit files contain 0.

FiberCost Axial B/op before→after:
- ForkJoin 2815→2812; ForkResource 4592→4590.
- ZipPar2 1280→1280; SequencePar8 3704→3704; SequencePar64 22296→22296.
- Race 1784→1784; TraversePar64 39360→39360; RunOnThreadPool 1181→1181.
- All Task controls unchanged.

ParallelComposition raw GC allocated bytes / operation count before→after:
- traversePar 10k / parallelism 8: 2887536→2887536.
- zipPar x1000: 1121214.84→1121214.84.
- sequencePar 10 flows x100: 563611.42→563611.42.
- race x1000: 1721214.84→1721214.84.

No measured B/op increases. Timing indicative only: ForkResource mean 5328.9→6263.8 ns (~18% higher); no timing improvement claimed. Full timing/control table in report.

## Phase 3 remaining — do not claim slice verified yet

1. Add/tighten torture coverage under examples/Axial.TortureTest for caller cancellation mid-cleanup and throwing observers. No new torture scenario or waiver yet; concurrency-source invariant still outstanding.
2. Final fail-first artifact after final Phase 3 commit:
   ```bash
   { git rev-parse --short HEAD; scripts/check-tests-fail-without-fix.sh origin/main tests/Axial.Tests 'FullyQualifiedName~UninterruptibleCleanupTests'; } > artifacts/verify/fail-first.txt
   ```
   Expect compile-red for the new internal primitive; keep the behavioral mutant evidence distinct. A stronger caller-token-propagation mutant can be added if desired, without altering original semantics.
3. Commit clean final state, then full detached verification per TOKEN-RULES:
   `setsid -f axial-verify > /home/adam/projects/scratch/axial-coord/pi/guardrails-primitives-verify.log 2>&1`.
   Poll with sleep 300; timeout/timing failures rerun once per rules before debugging.
4. Report `artifacts/verify/result.json` matching clean final HEAD. Fable, AOT, full solution build/test, docs and livedocs have NOT run in Phase 2. No result.json exists from Phase 2; focused test logs are not a substitute.
5. Run `/home/adam/projects/scratch/axial-coord/done-check.sh "$PWD"`; record DONE-CHECK PASS only when actually green.
6. Update handover/report, commit (no push), reply PHASE 3 DONE and stop.
