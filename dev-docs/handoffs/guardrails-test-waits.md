# AXG013 — narrowed before baseline

## State / stopping boundary

Worktree `/home/adam/projects/Axial/feature/guardrails-test-waits`, branch `feature/guardrails-test-waits`.
Commit locally; **do not push**. Read coordinator brief and TOKEN-RULES under
`/home/adam/projects/scratch/axial-coord/pi/` before continuing.

Completed the brief's final section, “narrow before baseline”. **Stop before any baseline or repository policy
approval.** No existing tests rewritten; no baseline/lock changed. Full report:
`/home/adam/projects/scratch/axial-coord/pi/guardrails-test-waits.report.md`.

## Narrowing decisions / changes

- `src/Axial.Guardrails/WaitForFactsAnalyzer.fs`: allow infinite duration (`Timeout.Infinite`,
  `Timeout.InfiniteTimeSpan`, literal `-1`) because completion waits for cancellation. Timeout fields resolve by
  symbol identity; similarly named user symbols do not qualify.
- Allow direct waits whose nearest enclosing loop is a while. For/foreach resets that exemption; a while inside
  a for allows polling again. Added numeric `for i = ... to ...` traversal, missing from the shared child walker,
  locally in this analyzer rather than changing other rules.
- Finite simulations, slow work, load generation and cancellation-deadline scheduling stay reported as directed.
  No aliases/data-flow analysis. Flow.sleep/Clock.sleep still out of scope (supplied clock unknown syntactically).
- `tests/Axial.Guardrails.Tests/WaitForFactsAnalyzerTests.fs`: tests first in commits `c031aa3b1`,
  `ef9776d0c` (type constraint correction), `2558d207f` (numeric-for coverage). Tests cover Thread/Task/Async,
  all infinite durations, bound waits, loop nesting both ways, post-loop waits, finite simulations and lookalikes.
- `docs/15-notes/04-guardrail-diagnostics.md` and `dev-docs/releases/0.12.0.md`: document the narrower rule.

## Evidence / policy blocker

Normal focused `dotnet test` still fails `axial check` because the previous wiring change touches protected
`src/Axial.Guardrails/build/Axial.Guardrails.targets`. No bypass flags or approval commands used.
The analyzer project **does build normally**:
`dotnet build src/Axial.Guardrails/Axial.Guardrails.fsproj -c Release --nologo -v q`.

Used FSI to load the new test source against the built analyzer and existing test dependencies/context helper,
then invoked its Fact/Theory cases. **Before fix: 28 passed, 17 failed. After fix: 45 passed, 0 failed.**
This is behavioral red/green; fixture snippets are typechecked/analyzed, not executed.
Runner and evidence under the coordinator directory:
- `guardrails-test-waits-narrow-tests.fsx`
- `guardrails-test-waits-narrow-red.log` (normal test build blocker)
- `guardrails-test-waits-narrow-red-direct.log`, `guardrails-test-waits-narrow-green-direct.log`
- `guardrails-test-waits-narrow-analyzer-build.log`

Copied rebuilt analyzer DLL into generated test output; ran the existing Guardrails assembly with `dotnet vstest`:
**129 passed, 0 failed** (3m). This assembly excludes new tests; those are covered separately by FSI.
Log `guardrails-test-waits-narrow-suite-direct.log`. Docs conventions passed (`guardrails-test-waits-narrow-docs.log`).
No full verify or new `artifacts/verify/result.json`; approvals still block whole-slice verification.

## Inventory: 43 → 25 sites

Re-ran the exact prior direct analyzer method: per-project cwd, Release analyzer, `--include-analyzers WaitForFacts`,
existing generated `.fscargs`/`.rules`; projects without args use direct `--project` with inventory-only rules
pointing to checked-in policy/baseline. All 15 test projects inventoried. No build/check override or baseline generation.

25 diagnostics / 25 unique sites in 8 files (was 43 in 13); no new sites. Drops:
- 12 infinite cancellation waits.
- 6 while-loop polls: ProcessServiceTests:31, ProcessStreamingTests:93, FiberMetricsTests:65,
  FiberObserverTests:45, TestSupport:223, WorkflowConcurrencyTests:453.

Project totals: Axial.Tests 21 → 8; Integration 19 → 15; Telemetry 1 → 0; Fable 1 → 1; HttpClient 1 → 1;
all ten other projects remain zero. Fable's finding is linked `examples/Axial.TortureTest/Interop.fs:35`.
Full per-file old → new table, remaining lines and every dropped site/reason are in the external report.
Intentional slow-server/load/deadline sites remain as directed, as do the two 500ms progress guesses at
ProcessServiceTests:1533,1535.

Persistent artifacts under coordinator directory:
`guardrails-test-waits-<Project>.before-narrow.sarif` preserves old inventory;
`guardrails-test-waits-<Project>.sarif` / `.inventory.log` now contain narrowed inventory.
`guardrails-test-waits-narrow-inventory.py` repeats old script with Axial.Tests included in the loop (previously
run separately). `guardrails-test-waits-inventory-status.json` covers all 15 projects: exit 254 with findings,
0 otherwise; no non-AXG013 errors found.

## Earlier context / exact next step

Previous wiring fix sets default test analyzer filter to `--include-analyzers Fixture WaitForFacts`, preserving
exclusion of ambient-effect rules; `all` is unchanged. Consumer regression `c38d2dd23` proved behavioral red/green.
That protected target needs human approval; the initial main merge was `a18c754f9` from `c394a4807`.

1. Coordinator/human reviews narrowed 25-site inventory and authorizes next phase.
2. Batch protected-target approval and AXG013 baseline adoption only as directed.
   **Do not run `axial policy baseline --rule AXG013` or repository `policy approve` without authorization.**
3. Once authorized/settled, run normal build/test and all `axial-verify` stages including livedocs, refresh final
   fail-first artifact and clean-HEAD `artifacts/verify/result.json`, then coordinator done-check.

No push. This is phase completion only; stop for fresh coordinator context.
