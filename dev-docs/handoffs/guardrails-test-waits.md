# AXG013 — wiring and classification handover

## State / stopping boundary

Worktree `/home/adam/projects/Axial/feature/guardrails-test-waits`, branch `feature/guardrails-test-waits`.
Commit locally; **do not push**. Read the coordinator brief and TOKEN-RULES under
`/home/adam/projects/scratch/axial-coord/pi/` before continuing.

Completed the requested exact-next-phase steps 1–3: default test-build wiring fix + regression test, direct
analyzer inventory with per-file counts including linked sources, external report. **Stopped before baseline
or repository lock approval.** No existing tests rewritten; no baseline or lock changed.

Full report (counts, line numbers, classification cautions, commands/evidence):
`/home/adam/projects/scratch/axial-coord/pi/guardrails-test-waits.report.md`.

## Changes / evidence

- Test-first commit `c38d2dd23` adds
  `PackagePropsTests.default test builds report guessed waits without enabling ambient effect rules`.
  It builds isolated test-role consumers and checks UtcNow remains permitted while awaited Task.Delay fails AXG013.
  Normal focused test run against the unfixed targets: **1 failed** (delay incorrectly built successfully).
- `src/Axial.Guardrails/build/Axial.Guardrails.targets` default test filter now uses the CLI's supported multi-value
  syntax: `--include-analyzers Fixture WaitForFacts`. Other effect analyzers remain excluded; `all` is unchanged.
- Normal post-fix test build is blocked by `axial check` because the target is protected. Direct check reports
  only `src/Axial.Guardrails/build/Axial.Guardrails.targets` under protected changes; all other categories `none`.
  This was expected and explicitly permitted by the coordinator's current instruction; no bypass flags used.
- Ran the already-built test-first assembly directly using `dotnet vstest` (not a build claiming policy passed).
  Consumer fixture builds import the changed targets normally. Focused regression: **1 passed**;
  full Guardrails assembly: **129 passed, 0 failed**, duration 3m23s.
  Existing fixture helpers approve only isolated temporary consumer policies, not this repository's lock.
- No full verify / `result.json` for this phase. Build and baseline approval remain prerequisites.

Logs under `/home/adam/projects/scratch/axial-coord/pi/`:
`guardrails-test-waits-wiring-red.log`, `guardrails-test-waits-wiring-green.log` (policy blocker),
`guardrails-test-waits-wiring-direct.log`, `guardrails-test-waits-suite-direct.log`,
`guardrails-test-waits-policy.log`, `guardrails-test-waits-cli-help.log`.

## Actual AXG013 inventory

**43 diagnostics across 13 source files**, 43 unique sites. All 15 `tests/*/*.fsproj` projects inventoried.
Project totals: Axial.Tests 21; Axial.Integration.Tests 19; Axial.Fable.Tests 1;
Axial.HttpClient.Tests 1; Axial.Telemetry.Tests 1; all ten others zero.
The Fable finding is linked `examples/Axial.TortureTest/Interop.fs:35` (not a test-role examples project finding).
Per-file counts and source lines are in the external report; do not infer inventory from the previous build's
single Fable finding.

Method: invoke Release analyzer directly with `dotnet fsharp-analyzers --include-analyzers WaitForFacts`,
existing generated `.rules` / `.fscargs`, and SARIF output. **Run from each project directory:** compiler source
paths are relative. A root-directory trial gave a false empty result; it was discarded/replaced by the correct
Axial.Tests run. Projects without generated args were loaded directly using `--project` and inventory-only rules
pointing at checked-in policy/baseline. No build/check override or baseline generation involved.
Saved inventory script, statuses, per-project SARIF/logs all live in coordinator directory:
`guardrails-test-waits-inventory.py`, `guardrails-test-waits-inventory-status.json`,
`guardrails-test-waits-<Project>.sarif`, `guardrails-test-waits-<Project>.inventory.log`.
Axial.Tests was run separately first, so is absent from the script's status JSON.

### Classification questions — do not blindly approve all findings

Current rule reports direct waits syntactically, not only proven readiness guesses:
- 12 findings are infinite token-cancelled suspension.
- `ProcessStreamingTests.fs:93` is a fact-poll with deadline, permitted by the brief but reported because its body
  contains Thread.Sleep. Existing analyzer does not infer surrounding polling semantics. This discrepancy needs
  coordinator/human direction before blanket baselining.
- `TestSupport.fs:223` belongs to the manual-time driver.
- `HttpServiceTests.fs:347` simulates slow server work; `WorkflowSchedulingTests.fs:40` generates pool load;
  `ProcessServiceTests.fs:1564` schedules cancellation at a timeout deadline.
- Clear guess: `ProcessServiceTests.fs:1533,1535`, two 500ms sleeps before comparing progress.

These were recorded, not rewritten or suppressed. Flow.sleep/Clock.sleep remain intentionally out of scope:
syntax cannot identify the supplied clock. Race arms, fact Wait/WaitAsync deadlines, and SpinUntil remain allowed.

## Earlier phase context worth retaining

Merged origin/main `c394a4807` in `a18c754f9`; fixture fixes `64799d315` made 128 existing Guardrails tests pass.
Previous coordinator-prescribed fail-first script against origin/main produced a compile red because main lacks
the AXG013 API, not behavioral red (`artifacts/verify/fail-first.txt`). This phase's new wiring test has a genuine
behavioral red and green in persistent logs. Policy role serialization, materialized fixture sources, corrected
indentation and assertions of successful typechecking prevent false-green analyzer fixtures.

Implementation uses symbol identities, test-role policy gating, shared baseline/suppression machinery and AXG002
registration. No alias/data-flow analysis is claimed. Docs/catalog/policy lists and release notes were added in
prior phases; executable docs still await full verification.

## Exact next phase

1. Coordinator/human reviews report and classification discrepancies. Any semantic narrowing is a separate,
   test-first change; do not silently rewrite existing tests or blanket-baseline permitted polling.
2. Batch human approval of the protected target change with AXG013 baseline adoption as directed.
   **Do not run `axial policy baseline --rule AXG013` or repository `policy approve` without authorization.**
3. Once approvals/classification are settled, run normal build/test and all stages via `axial-verify`, including
   livedocs; capture clean-HEAD `artifacts/verify/result.json`, refresh required final fail-first artifact,
   then coordinator done-check. Do not claim whole-slice completion before those pass.

This handover records phase completion only. No push; stop for fresh coordinator context.
