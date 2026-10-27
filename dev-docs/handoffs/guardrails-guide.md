# Guardrails guide — phase 3 handoff

STOP after phase 3. Fresh context for phase 4. This is phase completion, not whole-slice verification.

## Context

- Worktree: `/home/adam/projects/Axial/feature/guardrails-guide`, branch `feature/guardrails-guide`.
- Phase-2 checkpoint: `75946fffd`. Phase-3 implementation: `969a4bc49`.
- Tests-first commits: `fc8ba85c1`, input-type corrections `f759a5ea7` / `d322bc339`, expanded tests `0325b27d8`,
  repeated-checkpoint regression `3fa6726e4`.
- Brief: `/home/adam/projects/scratch/axial-coord/pi/guardrails-guide.md`; final owner decisions override earlier text.
- Report: `/home/adam/projects/scratch/axial-coord/pi/guardrails-guide.report.md`.
- No runtime/service source changes, TOML/lock/baseline edits, approvals, bypass properties, or push.

## Implemented phase-3 behavior

| Finding | Ordinary build | Checkpoint | Notes |
|---|---|---|---|
| AXG009 raw Task/TCS/CTS/Thread/ThreadPool/Run/StartNew/ContinueWith | Error | Error | Not accepted |
| AXG009 lock/Monitor/Interlocked | Warning, temporarily | Warning | Not accepted |
| AXG010 ignored catch-all exception | Warning | Error | Valid note keeps visible Info with reason |
| AXG011 CancellationToken.None | Warning, temporarily | Warning | Not accepted |
| AXG012 typed Task/ValueTask/Fiber discard | Error | Error | Not accepted |
| AXG013 Thread.Sleep in tests | Error | Error | Not accepted |
| AXG013 directly awaited delay in tests | Warning | Error | Valid note keeps visible Info with reason |
| AXG002 invalid note | Warning | Error | Malformed/empty/unknown/stale/misplaced/block-target note |

- Phase-2 role/runtime/helper scopes are preserved. AXG009 task bridges exempt declared runtime files,
  capability roles, tests and host tooling. Synchronization exempts ONLY declared runtime files, including when
  encountered in test/capability/tooling roles. Other AXG010–012 test/host-tooling exemptions remain.
- AXG011 and synchronization MUST remain warnings until the primitives lane merges; this session did not merge it.
  AXG011 still has no runtime-file exception: the eventual exception must be the ONE cleanup primitive.
- Guide syntax: `// axial-note AXG010: reason` or `// axial-note AXG013: reason`, on the finding's first line or
  immediately preceding line. Only ordinary line comments count. String/XML/block-comment lookalikes do not.
  Live guide coverage is checked after role/guardrail scoping. No boundary declaration or finding approval needed.
- AXG009–013 no longer honor `axial-allow` hiding or baseline severity downgrades. AXG002 rejects their legacy
  allowances. The old test.wait-for-facts allow/warn/deny override does not alter AXG013 tiers.
- AXG001–008 allowance/baseline behavior is unchanged. Note diagnostics use AXG002 without inheriting baseline
  downgrades; legacy AXG002 diagnostics retain their old behavior.
- Test-project default analyzer selection now includes Fixture, WaitForFacts, RawConcurrency, SuppressionIntegrity.
- No optional callback-under-lock heuristic, review command, collector, snapshot, or manifest was added.

## Checkpoint mechanism and CLI constraint

ONE MSBuild property: `AxialGuardrailsCheckpoint=true`, set by scripts/verify.sh's build commands and the release
workflow build. Ordinary Release builds are not checkpoints. Release also builds with --no-incremental.

The bundled CLI's `--treat-as-error` explicitly changes ALL severities for a code, including Info. It cannot
preserve noted Info or distinguish warning-level AXG002 notes from legacy AXG002. The implementation therefore
uses the equivalent targets path: serialize the property into the existing rules file (`setting checkpoint`),
and resolve only guide/invalid-note severity in the analyzer. No new environment variable or separate state file.
The existing AXIAL_GUARDRAILS_CONFIG transport is unchanged. This adaptation is recorded in the report.

Build output IS the review list. The checkpoint deletes only the analysis stamp so every checkpoint re-emits Info,
even if otherwise up-to-date. A committed consumer regression FAILED before this fix: --no-incremental alone is
not sufficient to guarantee a full review list with the existing content-based analyzer cache.

## Validation and fail-first evidence

Evidence: `/home/adam/projects/scratch/axial-coord/pi/guardrails-guide-phase3-*`.

- Analyzer project builds normally (0 warnings/errors).
- Final source-test runner: **204 passed / 0 failed**, `final-green.log`.
  Loads current concurrency, wait, hardening and config test sources with built SDK/xUnit/Axial dependencies from
  the earlier test-waits lane. Registered analyzer plugins are exercised; fixture type errors are rejected.
- Two real MSBuild + bundled CLI consumer tests: **2 passed / 0 failed**, `package-final-green.log`.
  Check development warnings, unnoted checkpoint errors, noted Info + reason, repeated checkpoint review output,
  invalid-note checkpoint errors, cache transition, and test-project RawConcurrency/SuppressionIntegrity selection.
  Consumers have no policy file: no policy approval or bypass is used. The FSI adapter only replaces repo-root and
  configuration discovery (AppContext.BaseDirectory differs); assertions and fixture builds are unchanged.
- Final committed tests replayed with ONLY phase-3 analyzer/targets changes shelved: **165 passed / 39 behavioral
  failures**, `replay-red.log` (exit 1), and **0 / 2 consumer tests**, `package-replay-red.log` (exit 1).
  Implementation was restored and rebuilt, followed by the final green runs. Replay script is beside these logs.
- Initial red evidence and separately committed repeated-checkpoint red: `red.log`, `notes-red.log`,
  `package-red.log`, `repeat-red.log`. Early unconstrained theory inputs were corrected before implementation;
  final replay contains behavioral failures, not generic reflection failures.
- Expanded FSI runner handles existing generic string theories and synchronous Facts. SDK fixture setup emits
  caught StructuredLogger/binlog cast warnings in FSI; no fixture typechecking errors are accepted.
- One combined final command hit its outer 300-second timeout after the first consumer test while sharing the
  dotnet lock. Rerun with a larger outer timeout passed both unchanged; not a test assertion failure.
- Docs conventions, bash -n scripts/verify.sh, and git diff --check passed. Executable docs examples unchanged.

## Protected-file blocker — human approval required, never bypass

Normal filtered dotnet test is blocked by axial check. Direct built-tool check reports exactly:
`src/Axial.Guardrails/build/Axial.Guardrails.targets` under protected files changed. Other categories are none.
This file contains inherited wiring plus phase-3 default analyzer selection, checkpoint rules setting, and forced
checkpoint analysis. No approval/baseline command was run. scripts/verify.sh and .github/workflows/release.yml
also changed to select checkpoints; the current policy check does not list them as protected.

No new artifacts/verify/result.json. Full verify/livedocs, the standard final fail-first script, and done-check
remain for phase 5 after human approval. Do not claim the whole slice passes.

## Phase 4 only, next session

Inventory findings in src/ and tests/ per rule/file. Include raw-task hits in services/host/domain code as bridge
work; propose a shrink-only to-do baseline only if they cannot move in this slice, without introducing baseline
hiding into the new tier logic. Do not change runtime adapters or promote AXG011/synchronization ahead of the
primitives merge. Preserve the lock blocker and report all protected changes for the human's final approval.
Do not start phase 5 or claim DONE for the full slice.

## Phase 4 inventory (done)

Full tables and per-file lists: `/home/adam/projects/scratch/axial-coord/pi/guardrails-guide.report.md`, section "Phase 4 inventory".
Evidence: `/home/adam/projects/scratch/axial-coord/pi/guardrails-guide-phase4*` (script, per-project logs/SARIF, rows JSON).
No source, TOML, lock or baseline changes; no approvals or bypass properties.

| Rule | Error | Warning |
|---|---|---|
| AXG009 raw task/TCS/CTS/thread | 25 | 0 |
| AXG009 lock/Monitor/Interlocked | 0 | 102 |
| AXG010 | 0 | 24 |
| AXG011 | 0 | 24 (about 0 after feature/flow-uninterruptible merges) |
| AXG012 | 5 | 0 |
| AXG013 | 9 (Thread.Sleep) | 9 (awaited delay) |

- Block-tier errors all sit in `src/Axial` runtime files (AXG009 25, AXG012 5) and `Thread.Sleep` in tests (9). They fail a normal build now.
  AXG009/AXG012 are not removed by feature/flow-uninterruptible; AXG009 needs exact-file runtime declaration (protected toml) or bridge moves; AXG012 and Thread.Sleep need code fixes.
- Gap: 8 test projects (and net8.0 of ElmishFlows/Telemetry/WebSocket) cannot be type-checked: their referenced libraries have no built output and a normal build fails on the errors above. Grep estimates are in the report.
- Next: phase 5 (verify, done-check) after human approves protected files.

## Phase 5 (pre-approval, done)

Runtime-core `[runtime] files` added (protected TOML); AXG012 now exempt there too; sync findings exempt in test role;
6 of 9 Thread.Sleep sites fixed, 3 left (RunOnTests:56, WorkflowParallelTests:168, WorkflowSchedulingTests:40: sleep is simulated work).
Analyzer tests green (see report "Phase 5"). Touched tests not run (lock). After human approval: build, run touched files 20x, `setsid -f axial-verify`, done-check.

## Phase 6 (pre-approval, done): name simulated work

Merged origin/main (flow-uninterruptible; no conflicts). AXG013 exempts `Axial.Tests.TestSupport.occupyThread` by symbol like
`waitUntil` (test written first, red then green; analyzer runner 205/0). Converted RunOnTests, WorkflowSchedulingTests,
HttpServiceTests, CrashTests (random pre-kill delay) to `occupyThread`; FiberMetricsTests poll loop to `waitUntil`;
WorkflowParallelTests overlap to a fact-wait (until peak >= 2 or all entered). Projects without a TestSupport module
(Files, HttpClient, Telemetry) link `tests/Shared/SimulatedWork.fs`.
NOT RUN (lock): Axial.Tests {RunOnTests, WorkflowSchedulingTests, WorkflowParallelTests}, HttpClient HttpServiceTests,
Files CrashTests, Telemetry FiberMetricsTests; none of these projects compiled.

### Exact steps after the human approves the protected files
1. Full build: `dotnet build Axial.sln -c Release` (confirm no Thread.Sleep AXG013 errors remain anywhere, incl. the 8 previously unanalyzed test projects).
2. Each touched test file 20x (filter by class): RunOnTests, WorkflowSchedulingTests, WorkflowParallelTests, HttpServiceTests, CrashTests, FiberMetricsTests, plus earlier phase-5 files (FiberObserverTests, WorkflowConcurrencyTests, ProcessServiceTests, ProcessStreamingTests).
3. `setsid -f /home/adam/projects/scratch/axial-coord/limits/bin/axial-verify > /home/adam/projects/scratch/axial-coord/pi/guardrails-guide-verify.log 2>&1`; poll artifacts/verify/result.json (HEAD, dirty=false).
4. `/home/adam/projects/scratch/axial-coord/done-check.sh $PWD` must print DONE-CHECK PASS.
5. Fail-first on the final commit: `{ git rev-parse --short HEAD; scripts/check-tests-fail-without-fix.sh origin/main <project> "<filter>"; } > artifacts/verify/fail-first.txt`.

## Phase 7 (post-approval): BLOCKED at step 1

`dotnet build Axial.slnx -c Release` (the repo has no Axial.sln) fails with 31 errors (15 unique AXG009, 1 AXG012, 1 AXG002, 1 FS0041).
The approved toml declares `[runtime]` only for src/Axial files; these services/host/benchmark files still raise AXG009 (raw TCS/Task.ContinueWith/WhenAny)
and need either a protected declaration or bridge moves into capability-role modules:
- src/Axial.ElmishFlows/Sub.fs (17, 18, 59), src/Axial.ElmishFlows/TestRuntime.fs (88)
- src/Axial.Hosting/Hosting.fs (164, 167; AXG012 at 257 `ignore fiber`)
- benchmarks/Axial.Benchmarks/FiberCostBenchmarks.fs (90-100), Suites.fs (347, 452)
- AXG002 stale suppression at line 519 of an Axial.Tests file (file not named in the message; not reachable until the build gets further)
Fixed: FS0041 in tests/Axial.Integration.Tests/ProcessServiceTests.fs (annotate `timeout: TimeSpan`).
Steps 2-5 not run. Because ElmishFlows/Hosting fail, dependent test projects still cannot be built.

## Phase 8: adapters

### Phase 1 (implementation complete; adapter test validation blocked)

- Tests-first checkpoint: `5f6a526e5` adds subscription disposal tests and benchmark analyzer tests.
- Benchmark host projects under root `benchmarks/` now exempt AXG009/AXG012 only. Tests prove nested lookalikes and services roles remain checked, as do AXG010/AXG011. Red: 2 failures / 2 passes before fix; green: all 112 ConcurrencyHazards tests pass.
- Subscription tests pin non-blocking disposal with a gated finalizer, interruption of a started fiber, eventual finalizer completion, and immediate disposal before startup settles. Existing subscription delivery tests remain.
- Hosting shutdown test now covers both gated cleanup completion and abandoned-token completion, checking that only abandonment logs a warning.
- AXG012: heartbeat fork uses an anonymous CE bind; the same enclosing Flow scope owns it. Removed the no-op `finally ignore fiber`, not ownership or cancellation.
- AXG002: removed stale inline AXG007 comment at ScopedResourceOwnershipTests.fs:519. All 23 ScopedResourceOwnershipTests pass.
- Adapter test runs were attempted on unchanged Sub/TestRuntime/StopAsync implementations, but compilation is blocked by the existing AXG009 errors (Sub 17/18/59, TestRuntime 88, Hosting 164/167). Hosting no longer reports AXG012. No bypass or protected edits used. New adapter test execution must be completed before/alongside phase 2; passing on the old adapter implementation has NOT been established.
- No adapter conversion, full solution build, 20x runs, full verify, done-check, or final fail-first script run in this phase. No result.json exists from this phase. Those final gates remain phase 3. Torture waiver records that the phase-1 Hosting edit changes only the unused bind; phase 2 still requires a torture scenario.
- Logs and phase report: `/home/adam/projects/scratch/axial-coord/pi/adapters-*.log`, `guardrails-adapters.report.md`. Stop here; phase 2 needs fresh context.

### Phase 2 (complete; stop before phase 3)

- Coordinator's old-adapter proof completed first: throwaway detached worktree at origin/main e7a1befea,
  copied only SubscriptionTests.fs and HostingTests.fs. Built its analyzer normally, then SubscriptionTests **6/6**,
  HostingTests **18/18** passed. Restored copied tests and removed the clean worktree. Logs: adapters-old-*.log.
- Sub uses a Deferred stop signal and scoped watcher fiber. Disposal signals synchronously without blocking;
  early signals are retained until startup, and the owning scope waits for stream finalizers. Removed unused
  completion bookkeeping and raw TCS/ContinueWith. Dispatch suppression remains atomic.
- startLayer uses Deferred completion and Deferred.toTask; provisioning failures, defects and interruption
  retain their existing boundary behavior. No public API or policy change.
- Hosting StopAsync races Deferred waits: the already-started stop operation retains cleanup ownership.
  Deferred.fromTask observes its completion; cancelling the losing wait cannot cancel or join ongoing cleanup.
  Abandonment still logs the warning. Faults remain defects and propagate at the Task boundary.
- Torture interop extended with callback-before-start and gated-cleanup/abandoned-wait invariants.
  Initial scenario hung: seed 1 reached the abandonment race, whose Fiber.awaitExit loser ignores cancellation;
  Flow.race waits for its loser, while the finalizer gate was released only after that race. This was a scenario
  deadlock, not evidence of a lost wake-up. Reproduced under timeout (exit 124; adapters-hang-repro.log),
  then replaced the uncancellable observer with a Deferred completion signal (same approach as Hosting).
  No runtime semantics changed or invariant weakened. Debug probes removed. Interop **10/10 rounds** pass.
- Final validation: ElmishFlows full suite **29/29**, HostingTests **18/18**; full
  `dotnet build Axial.slnx -c Release` succeeds with **0 errors** (8 warnings in incremental final run).
  git diff --check passes. All subsequent test/torture runs use timeout; keep that in phase 3.
- Phase 3 still owns 20x runs, full detached axial-verify, done-check and final fail-first proof.
  No new artifacts/verify/result.json; no full-slice verification claim. No protected edits, approvals,
  baseline changes, suppressions, new runtime files, or push. STOP after this phase's commit.
