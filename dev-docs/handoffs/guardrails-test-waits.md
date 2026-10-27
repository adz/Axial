# AXG013 — unblocked execution phase handover

## State

Worktree: `/home/adam/projects/Axial/feature/guardrails-test-waits`.
Branch: `feature/guardrails-test-waits`. Commit locally; do not push.
Read `/home/adam/projects/scratch/axial-coord/pi/guardrails-test-waits.md`, including its final unblocked section,
and TOKEN-RULES.md before continuing.

Merged `origin/main` at `c394a4807` in merge commit `a18c754f9`. The release-note conflict retained both AXG013 and
lock-format-v2 breaking changes. Analyzer tests now execute under the approved v2 lock without bypassing policy.
Fixture corrections committed as `64799d315`; no implementation change was needed to make the analyzer suite green.
No baseline edit/generation, approval command, push, existing-test rewrite, or full verify was performed.

## Fixes and evidence

`tests/Axial.Guardrails.Tests/WaitForFactsAnalyzerTests.fs`:
- Explicitly serialize all five role cases. `string role` had emitted `Axial.Guardrails.Policy+Role`, not `test`.
- Materialize the fixture source so policy boundary-path validation finds `Fixture.fs`.
- Correct F# fixture indentation (CE steps and ordinary statements); keep every behavioral assertion.
- Assert typecheck diagnostics contain no errors, including in allowed fixtures, so unresolved symbols cannot
  produce false-green absence checks.

`dotnet test tests/Axial.Guardrails.Tests/Axial.Guardrails.Tests.fsproj -c Release --nologo -v q`:
**128 passed, 0 failed** (entire Guardrails suite, including all 21 AXG013 cases).
Log: `/home/adam/projects/scratch/axial-coord/pi/guardrails-test-waits-suite.log`.

Requested fail-first command succeeded against `origin/main` with filter
`FullyQualifiedName~WaitForFactsAnalyzerTests`. Its result is a compile failure because the base lacks the analyzer
API, not a behavioral red; the tests themselves assert reported forms, allowed forms, role gating, message guidance,
and suppression locality. `artifacts/verify/fail-first.txt` records the command and commit, refreshed after this
handover commit. This is the coordinator-prescribed script; no independent policy-check bypass was added.

## Solution build: expected baseline blocker, plus wiring gap

Final `dotnet build Axial.slnx -v minimal -nologo` exited 1 after 4m50s:
- AXG013 at `examples/Axial.TortureTest/Interop.fs:35:20`, compiled as a linked file by
  `tests/Axial.Fable.Tests/Axial.Fable.Tests.fsproj`.
- The second build error is the enclosing analyzer target reporting that finding.
- No compiler errors or other diagnostics remain in that build.

Log: `/home/adam/projects/scratch/axial-coord/pi/guardrails-test-waits-build.log`.
Exit marker: `/home/adam/projects/scratch/axial-coord/pi/guardrails-test-waits-build.exit`.
An earlier quiet build failed at policy invocation, but a direct `dotnet --roll-forward Major ... check` passed
and subsequent normal test/build commands passed policy checking unchanged. An intervening build attempt timed out
waiting for the shared dotnet lock; the final attempt ran detached and completed. Do not confuse that queue timeout
with the final baseline finding.

**WIRING GAP FOUND, NOT FIXED:** `src/Axial.Guardrails/build/Axial.Guardrails.targets:20–24` selects only
`--include-analyzers Fixture` for ordinary `IsTestProject=true` projects. Thus AXG013 currently runs in the Fable
project (which lacks that flag), but is filtered out of normal xUnit project builds. Analyzer unit tests do run it
explicitly. Before preparing the actual baseline count, include WaitForFacts in the default test analyzer filter
using the CLI's supported multi-pattern syntax, add a package/build-wiring regression test, and check whether the
build-target change needs human lock approval. Do not disable policy checking, generate a lock, or infer that the
single current build finding is the complete baseline inventory. Leave unrelated effect analyzers excluded by default.

## Existing implementation decisions

- `WaitForFactsAnalyzer.analyzeWithConfig` runs only for resolved policy role `test`, key `test.wait-for-facts`,
  default deny; honors allow/warn/deny/forbid and shared baseline/site suppression machinery.
- Symbol identities distinguish Thread.Sleep, Task.Delay, FSharpAsync.Sleep from unrelated lookalikes.
  Thread.Sleep calls are reported; Task.Delay/Async.Sleep only as direct do!/let! sources. Wrapped direct sources
  are recognized; no helper/alias data-flow analysis is claimed.
- Deadline arms are not direct awaited delays, so remain allowed. Fact waits/polling are not targets.
- Flow.sleep / Clock.sleep are deliberately out of scope because syntax does not identify the supplied clock.
- AXG002 recognizes AXG013 and uses raw sites to validate suppressions. Catalog and both policy allowlists register
  the rule. Docs/index/release notes were added in phase 2; executable docs remain unverified.

## Exact next phase / stopping boundary

1. Fix and test the default test-build analyzer wiring above; if protected-target changes invalidate the approved
   lock, stop for the coordinator/human rather than bypassing the check or approving it yourself.
2. Prepare phase-3 classification and **per-file AXG013 counts**, including linked sources, with actual analyzer
   coverage of normal test projects. Do not rewrite existing tests in this slice.
3. STOP before `axial policy baseline --rule AXG013`; baseline generation is a HUMAN DECISION. No baseline approval
   has been requested or granted yet. Write the count/report to
   `/home/adam/projects/scratch/axial-coord/pi/guardrails-test-waits.report.md`.
4. After human approval only: baseline/merge as directed, full `axial-verify` (all stages including livedocs), final
   fail-first artifact, clean-HEAD `artifacts/verify/result.json`, and coordinator done-check. No result.json was
   produced in this focused phase. This handover reports phase completion, not full slice verification.
