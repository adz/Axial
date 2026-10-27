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
