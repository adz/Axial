# Guardrails internal — executable validation handover

## Phase state

Merged `origin/main` at `99fa396f13e1ecab53029712a577040d7c5f7e4f` in merge commit `050abac57`.
The coordinator's final **unblocked** instruction has been followed. Focused tests and fail-first are now established;
this supersedes the previous handover's unrun/wait instructions. This is a phase boundary, NOT final slice completion.
No push. No runtime behavior changes, repository suppressions, baseline generation, policy approval, or lock edits.

## Changes this phase

- Merge resolution preserves BOTH `Policy.Config.Runtime` and main's `SuppressionIgnores`, including parsing and
  validation. The repository `Axial.Guardrails.toml` now matches main exactly: `[runtime]` is deliberately absent
  until phase 3. The previous handover said it was deferred, but the actual phase-2 commit still contained it;
  this merge really removes it. Keep runtime parser, lock handling and fixture tests.
- Preserved both release-note entries (AXG009–AXG012 and lock version 2).
- Added `string list` annotation to runtime path validation to fix FS0072 compilation errors.
- Fixed test plugin discovery: CLI analyzer bindings compile as attributed static METHODS, not properties.
  Tests invoke every `CliAnalyzerAttribute` method, then filter diagnostics by code. The original reflection
  helper found no plugins, so its initial failures were fixture failures, not meaningful analyzer RED evidence.
- Fix commit: `e3f9e3dca`.

## Actual validation

All logs below are local generated files in `artifacts/verify/` and remain untracked.

- `dotnet build src/Axial.Tool/Axial.Tool.fsproj -c Release -v q -nologo`: passed, no warnings/errors.
  Important after merging: the initially cached Release policy tool did not understand `[suppressions]`.
  Rebuilding it resolves that; do not bypass policy checks.
- `dotnet --roll-forward Major artifacts/bin/Axial.Tool/release/Axial.Tool.dll check`: passed.
- `dotnet build src/Axial.Guardrails/Axial.Guardrails.fsproj -v q -nologo`: passed, no warnings/errors
  (`internal-analyzer-build.log`).
- Focused Debug tests: **76 passed**, filter
  `FullyQualifiedName~ConcurrencyHazards|FullyQualifiedName~PolicyLockTests`, with
  `-p:BuildProjectReferences=false` (`internal-tests.log`). This builds/runs the analyzer test project using
  already compiled project-reference outputs; it does not disable the policy check or any analyzer.
- Full **Release** Guardrails test suite: **159 passed, 0 failed, 0 skipped**, using
  `dotnet test tests/Axial.Guardrails.Tests -c Release --nologo -v q -p:BuildProjectReferences=false`
  (`internal-release-tests.log`). Release is the repository verification configuration.
- Fail-first script against `origin/main`, same focused filter: **OK: the tests fail**, with actual assertion
  failures for AXG009–AXG012, suppression/role witnesses, runtime exemption and runtime lock protection.
  `fail-first.txt` records HEAD and the script result; it is regenerated after the handover commit.
- Full Debug suite additionally tried: 158 passed, one failure in the unchanged
  `PackagePropsTests.project reference content invalidates analysis without volatile timestamp invalidation`.
  Reproduces alone at line 393 (body-only dependency edit did not trigger analysis). Both that test and its
  targets are identical to main. The same test passed against main's sources in Release, and the full current
  Release suite passed. Do not describe the Debug suite as green. Details: `internal-package-detail.log`.

## Remaining build/approval boundary

Both normal Debug and Release `dotnet build Axial.slnx` compile the implementation, run the policy check, then
FAIL on existing-source AXG009–AXG012 findings: 157 build errors including analyzer-failure summaries. Logs:
`internal-build.log`, `internal-release-build.log`. Debug log has 148 distinct file/line/column/rule/message sites:
AXG009 93, AXG010 21, AXG011 24, AXG012 10 (some constructor symbol ranges produce multiple diagnostics on one
line). This is a PARTIAL inventory: failing project dependencies prevent the solution from analyzing everything.
Do NOT treat these counts as the classification or proposed baseline counts. No baseline/suppression was added
just to make the build green. Finishing the solution build requires the separately authorized phase-3 work.

`artifacts/verify/result.json` does not exist for this phase. Full `axial-verify` and done-check have NOT passed;
they remain approval-dependent. The phase's green result is the full Release analyzer suite, not the solution.

## Existing implementation decisions retained

- Four analyzers AXG009–AXG012; all exempt resolved `test` role, all other roles report errors.
- AXG010 is a documented heuristic: bound exception use, raise/reraise, or call names containing
  Report/notify/Observer/Log/Record count as handling; not path-sensitive proof.
- Reasoned per-site allowances need no boundary membership; AXG002 validates reason, locality and staleness.
- `[runtime].files` accepts exact existing in-root paths, no globs; exempts AXG009 only. Adapter boundary membership
  never exempts AXG009. PolicyLock tracks runtime entries as `runtime:`-prefixed boundary entries.
- No AXG013 changes; another lane owns that rule.

## Exact next phase: classification and approval preparation

1. Read the external brief including its final coordinator decisions. Phase 3 is now next, not more waiting for
   lock-format approval. Re-add the proposed runtime list in the SAME approval batch as source suppressions and
   baseline preparation. Allowed proposed exact files:
   `src/Axial/{App,Cache,Core,Flow,Hub,Parallelism,Platform,Queue,Ref,Stream}.fs`.
   No operational adapters. Re-adding policy entries intentionally makes the lock stale until human approval.
2. Classify EVERY finding in `src/` in
   `/home/adam/projects/scratch/axial-coord/pi/guardrails-internal.report.md` as a table of file:line, rule,
   legitimate/suspicious, and a specific true reason. The current build logs are only a starting inventory.
   Add per-site suppressions only for justified legitimate sites. Do not fix runtime behavior in this slice.
3. Suspicious/unjustifiable sites need proposed shrink-only baseline counts. **STOP before running either
   `axial policy baseline` or `axial policy approve`**; those are human decisions. Never bypass `axial check`.
4. After coordinator/human approval, complete solution build, full `axial-verify`, HEAD-matching fail-first
   artifact, clean `result.json`, done-check. Shared dotnet commands serialize; use detached processes for long
   waits (one full verification in another lane held the lock for ~20 minutes during this phase).
5. Update/commit this handover at each phase boundary; keep it until all useful content is captured elsewhere.
