# AXG013 — polls only via an approved helper

## State / stopping boundary

Worktree `/home/adam/projects/Axial/feature/guardrails-test-waits`, branch `feature/guardrails-test-waits`.
Commit locally; **do not push**. Read the coordinator brief and TOKEN-RULES under
`/home/adam/projects/scratch/axial-coord/pi/` before continuing.

Completed the brief's latest section, “polls only via an approved helper”. **Stop before any baseline or repository
policy approval.** No existing tests rewritten; no baseline/lock changed. Full report:
`/home/adam/projects/scratch/axial-coord/pi/guardrails-test-waits.report.md`.

## Decisions / files changed

- `src/Axial.Guardrails/WaitForFactsAnalyzer.fs`: reversed the nearest-while exemption. All bare finite sleeps in
  loops are reported again. Only the body of the module binding `Axial.Tests.TestSupport.waitUntil` is exempt,
  identified by its definition symbol and binding-body range. Other modules, siblings and local lookalikes do not
  qualify. Kept numeric-for traversal, infinite-cancellation allowance and all previous fact-deadline allowances.
- `tests/Axial.Tests/TestSupport.fs:23`: added `waitUntil (condition: unit -> bool) (deadline: TimeSpan) : bool`.
  Uses Stopwatch and a 1ms polling sleep, returns true on readiness / false on expiry, rejects negative bounds,
  propagates predicate exceptions. No existing callers migrated. Only this helper's internal sleep is exempt.
- `tests/Axial.Guardrails.Tests/WaitForFactsAnalyzerTests.fs`: tests first in `7f0f2137c`, annotation correction
  `834071969`. Reversed prior while-loop assertions, added counting-loop coverage and fully qualified helper
  identity/body-scope tests for Thread/Task/Async. All prior infinite/deadline/suppression/role tests retained.
- `docs/15-notes/04-guardrail-diagnostics.md` and `dev-docs/releases/0.12.0.md`: describe approved-helper-only polls.
- Flow.sleep/Clock.sleep remain out of scope: supplied live versus manual clock is unknown syntactically.

## Evidence / policy blocker

Normal focused `dotnet test` still fails `axial check` because the earlier wiring change touches protected
`src/Axial.Guardrails/build/Axial.Guardrails.targets`. No bypass flags or approval commands used.
Analyzer builds normally:
`dotnet build src/Axial.Guardrails/Axial.Guardrails.fsproj -c Release --nologo -v q`.

Used FSI to load the current test source against the built analyzer and existing test dependencies/context helper,
then invoked Fact/Theory cases. **Before fix: 40 passed, 10 failed. After fix: 50 passed, 0 failed.** Behavioral
red/green: snippets typechecked/analyzed, not executed. Actual TestSupport.fs loaded via FSI: **7 helper runtime
checks passed**. Copied rebuilt analyzer DLL into generated test output and ran the existing Guardrails assembly
with `dotnet vstest`: **129 passed, 0 failed**. That old assembly excludes new tests, separately covered by FSI.
Docs conventions and `git diff --check` passed. No executable docs example changed.

Evidence under coordinator directory:
- `guardrails-test-waits-helper-red.log` (normal test policy blocker).
- `guardrails-test-waits-narrow-tests.fsx` (reusable direct source runner).
- `guardrails-test-waits-helper-red-direct.log`, `guardrails-test-waits-helper-green-direct.log`.
- `guardrails-test-waits-helper-build.log`, `guardrails-test-waits-helper-suite-direct.log`,
  `guardrails-test-waits-helper-docs.log`, `guardrails-test-waits-helper-runtime.fsx` / `.log`.

No full verify or new `artifacts/verify/result.json`; approvals still block whole-slice verification.

## Inventory: 25 → 31 sites

Repeated the exact prior read-only inventory via `guardrails-test-waits-narrow-inventory.py`: per-project cwd,
Release analyzer, `--include-analyzers WaitForFacts`, existing generated `.fscargs`/`.rules`; projects without args
use direct `--project` evaluation with inventory-only rules pointing to checked-in policy/baseline. All 15 test
projects covered. No build/check override or baseline generation.

31 diagnostics / 31 unique sites in 11 files (was 25 in 8). All six prior while-loop exemptions returned:
- ProcessServiceTests:31; ProcessStreamingTests:93.
- FiberMetricsTests:65; FiberObserverTests:45.
- TestSupport:237 (was 223; helper insertion adds 14 lines).
- WorkflowConcurrencyTests:453.

The 12 infinite cancellation waits remain absent. Added waitUntil's internal sleep is absent as intended.
Project totals: Axial.Tests 8 → 11; Integration 15 → 17; Telemetry 0 → 1; Fable 1 → 1; HttpClient 1 → 1;
all ten others remain zero. Full per-file counts/current lines in external report.

Artifacts: `.before-helper.sarif` preserves 25-site inventory, `.before-narrow.sarif` preserves original 43 sites;
`guardrails-test-waits-<Project>.sarif` / `.inventory.log` now contain helper-only inventory.
`guardrails-test-waits-inventory-status.json` covers all 15 projects: exit 254 with findings, 0 otherwise.

## Earlier context / exact next step

Prior wiring fix sets default test analyzer filter to `--include-analyzers Fixture WaitForFacts`, preserving
exclusion of ambient-effect rules; `all` is unchanged. Consumer regression `c38d2dd23` proved behavioral red/green.
That protected target needs human approval; initial main merge was `a18c754f9` from `c394a4807`.

1. Coordinator/human reviews current 31-site inventory and authorizes the next phase.
2. Batch protected-target approval and AXG013 baseline adoption only as directed.
   **Do not run `axial policy baseline --rule AXG013` or repository `policy approve` without authorization.**
3. Once authorized/settled, run normal build/test and all `axial-verify` stages including livedocs, refresh final
   fail-first artifact and clean-HEAD `artifacts/verify/result.json`, then coordinator done-check.

No push. This is phase completion only; stop for fresh coordinator context.
