# Guardrails guide — phase 2 handoff

STOP after phase 2. Fresh context for phase 3. This is phase completion, not whole-slice verification.

## Context

- Worktree: `/home/adam/projects/Axial/feature/guardrails-guide`, branch `feature/guardrails-guide`.
- Phase-2 checkpoint: `766ed97ff` (phase-1 merge/revert/design checkpoint).
- Tests-first commits: `f06cf0da7`, string input correction `b3e53736c`, overload/ignore coverage `c0af0421a`,
  synchronization scope `e19c09aaa`, nested-ignore witness `4798cb319`. Implementation follows these commits.
- Brief: `/home/adam/projects/scratch/axial-coord/pi/guardrails-guide.md`; its coordinator/owner sections override
  the phase-1 proposal. Full current report: `guardrails-guide.report.md` beside that brief.
- No runtime/service source changes, policy/lock/baseline edits, approvals, bypass properties or push.

## Phase-2 behavior

- AXG009 uses exact BCL owners, includes Task construction, and exempts capability-role task bridges and declared
  runtime files. Test and host-tooling roles remain exempt for task bridges. Tooling means host-role projects under
  `src/Axial.Guardrails` or `src/Axial.Tool*`, not every host.
- Latest synchronization refinement wins: lock/Monitor/Interlocked warn everywhere except declared runtime files,
  including tests, capabilities and host tooling. Error promotion waits for the primitives lane; no optional
  callback-under-lock heuristic added.
- AXG010 reports only catch-all handlers ignoring the exception, resolving `exn`/System.Exception type aliases and
  excluding narrower exception types. Existing exception uses/guards/propagation count. Reporting-like function
  names alone no longer exempt a handler.
- AXG011 matching is unchanged, but severity is temporarily Warning until primitives merge. Existing runtime
  declarations do not yet exempt CancellationToken.None; the eventual exception must be the ONE primitive.
- AXG012 detects typed Task/ValueTask/Fiber discards only; Async.Start/StartImmediate no longer reported. Direct and
  piped F# ignore resolve by symbol; same-named user functions and nested ignore calls in retaining lambdas do not
  trigger. Cold Flow descriptions and awaited work remain out of scope.
- AXG013: infinite Thread.Sleep and tokenless infinite Task.Delay report. Infinite Async.Sleep and Task.Delay
  overloads with a cancellation-token parameter are allowed (including TimeProvider + token, not TimeProvider
  alone). Bounded SpinUntil overloads/alias, bare-loop sleeps, and the one symbol-qualified
  Axial.Tests.TestSupport.waitUntil helper are tested. The helper's exemption is its symbol and binding-body range.
- Diagnostic guide and 0.12.0 release notes updated; executable examples unchanged.

## Tests / blocker

- Source-test runner using the built live analyzer and current test sources: initial red **112 passed / 21 failed**;
  additional overload/identity red **134 / 4**; synchronization red **135 / 3**; nested-ignore filtered red **12 / 1**.
- Final green: **139 passed / 0 failed**. Final current-tests-without-fix replay: **114 passed / 25 behavioral
  failures** (exit 1). Only the two implementation files were shelved; committed tests stayed in place. Both files
  were restored unchanged afterward. No implementation changed after the green run.
- Persistent runner/logs: `/home/adam/projects/scratch/axial-coord/pi/guardrails-guide-phase2-*`.
  FSI loads current AnalyzerTestContext + both test files, built SDK/xUnit/Axial fixture dependencies from the earlier
  test-waits lane, and this worktree's current analyzer. This is not a normal compiled project test run.
- SDK fixture options emit caught StructuredLogger binlog/cast messages in FSI; fixtures then use loaded runtime
  references and are typechecked with no accepted fixture errors.
- Analyzer project builds normally. Docs conventions and `git diff --check` passed.
- Normal filtered dotnet test is blocked by policy check. Direct `dotnet --roll-forward Major
  artifacts/bin/Axial.Tool/release/Axial.Tool.dll check` identifies only the inherited protected change:
  `src/Axial.Guardrails/build/Axial.Guardrails.targets`. No downgrade/baseline/suppression/boundary additions.
  Human approves once at the end; do not self-approve or bypass.
- No new `artifacts/verify/result.json`; all-stage verify, livedocs, standard final fail-first script and done-check
  remain for phase 5, after policy approval. Do not claim this phase proves the full slice.

## Phase 3 only, next session

Implement the simpler owner-approved tier/notes/checkpoint model with tests first:

- Guide tier = AXG010 and awaited-delay AXG013. Warning without a note, visible Info with reason for a valid note;
  checkpoint promotes unacknowledged guide findings using the analyzer CLI option, behind ONE property set only
  by checked-in verify/release scripts. Build output is the review list; no snapshots/collector/manifests.
- Block tier = raw-task AXG009, typed AXG012, Thread.Sleep AXG013, and (after primitives merge) AXG011 and
  synchronization AXG009. No axial-note exemptions for block findings. Keep the two temporary warning groups
  temporary until the primitives lane merges; do not promote them through the guide checkpoint switch.
- AXG002 validates guide notes, live finding coverage and nonempty reason. Invalid notes warn in development and
  fail checkpoint. Preserve AXG001–008 behavior.
- Imported AXG009–013 legacy allowance/baseline hiding and old advice/severity messages still need replacement in
  phase 3. Block rules must be role-scoped, not downgraded by allowances, baselines or the old test.wait-for-facts
  allow/warn override. AXG010 and awaited AXG013 retain imported severity in this narrowing-only phase.
- Test-project targets currently select Fixture + WaitForFacts only. Include RawConcurrency in the phase-3 wiring
  so the latest synchronization scope also runs in normal test builds.
- List protected target changes for human approval; do not approve/baseline/bypass.

Do not start phase 4 inventory or phase 5 verification in this session.
