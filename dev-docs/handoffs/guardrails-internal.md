# Guardrails internal — phase 3 classification / approval preparation

## State

Phase 3 prepares the human approval batch; phase 4 (full verification) is NOT done. No push, policy approval,
baseline-generation command, baseline edit, lock edit, or runtime behavior change. The current policy check
correctly blocks consumer builds. Do not run `axial policy baseline` or `axial policy approve` without the
coordinator/human decision.

The complete classification table and evidence are in:
`/home/adam/projects/scratch/axial-coord/pi/guardrails-internal.report.md`.
That directory also contains scan drivers, SARIF snapshots, classification JSON and report-generation inputs.

## Changes and decisions

- Re-added the exact agreed `[runtime].files` list:
  `src/Axial/{App,Cache,Core,Flow,Hub,Parallelism,Platform,Queue,Ref,Stream}.fs`.
  It exempts AXG009 only; no adapter is runtime-exempt.
- AXG010 recognizes exception use through assignments, error-record fields, chained call receivers, and guards.
  Only its handler-inspection traversal changes; handler discovery does not expand. Returning a constant
  `Error`/`Exit.Failure` does NOT itself suppress a finding. The heuristic remains documented, not path-sensitive.
- AXG012 removes the name-only `fork` fallback: `Flow.fork` returns a cold description, not started work. Typed
  Task/ValueTask/Fiber discards remain checked. Awaited bindings are identified through FCS `LetBang`/`UseBang`/
  `AndBang` trivia. Array/list Sequential nodes describe retained elements, not statements. Explicit casts are
  followed when typed lowering erases the cast's source range; actual cast task discards still report.
- Added reasoned per-site allowances at 84 legitimate sites (AXG009 29 / AXG010 30 / AXG011 25 / AXG012 0).
  All operational/runtime source edits are comments only; normalized-source comparison confirmed this.
- No allowance or behavior fix at the 63 suspicious sites. These are a baseline PROPOSAL, not approved entries.
- Updated diagnostic guide and 0.12.0 release notes for the narrowed behavior. AXG013 is untouched.
- Tests were committed before the implementation checkpoint in `e15bf33c9`, `0f7b8854a`, `521078716`,
  `bf32df97e`, `924688b89`, `0a0fa0d8a`. Fixture helpers now reject parse/type errors. A cancellation type-test
  guard needs parentheses or F# parses its arrow as a function type; the fixture was corrected accordingly.

## Inventory and counts

Read-only CLI scans cover all 18 src projects, including both HTTP host adapters outside Axial.slnx, and both
of every project's declared .NET target frameworks. Initial inventory: 19 runs (10 cached compiler-argument inputs,
9 project evaluations), plus 10 explicit net8 project evaluations. No baseline output setting or policy override.
This is not a FABLE_COMPILER inventory. The initial snapshots use the phase-2 analyzer; narrowed snapshots use
this phase's normally built Release analyzer.

| Rule | Before narrowing, after runtime list | Narrowed away | After narrowing | Allowances | Baseline proposal |
|---|---:|---:|---:|---:|---:|
| AXG009 | 65 | 0 | 65 | 29 | 36 |
| AXG010 | 59 | 12 | 47 | 30 | 17 |
| AXG011 | 25 | 0 | 25 | 25 | 0 |
| AXG012 | 13 | 3 | 10 | 0 | 10 |
| Total | 162 | 15 | 147 | 84 | 63 |

Both narrowed scans returned exactly the 63 suspicious sites, no missing or extra sites. The final repeat after
cast-range handling completed all 29 project/target runs. Evidence: `guardrails-internal-narrowed-*.sarif`, both
narrowed status JSON files, and `guardrails-internal-final-sites.json`. Counts are finding sites, not generated fingerprint rows: identical
fingerprints may require `xN` baseline counts. No baseline has been generated.

Removed AXG010 sites: assignment recording (5), cancellation guards (2), chained exception rethrow (4), typed
error record (1). AXG012: awaited wildcard bindings (2), retained array element (1). Cold fork narrowing removes
no existing src finding, but has regression coverage.

## Validation established so far

The earlier handover's blanket assumption that ALL builds/tests are blocked by the stale lock was wrong:
`Directory.Build.targets` naturally excludes the analyzer itself, and `Directory.Build.props` already excludes
its fixture project. No enforcement setting was changed and no bypass property was supplied. We can build the
analyzer normally and test its fixture project using already compiled references while consumer builds remain
blocked. Do not disable checks for any other project.

- Normal `dotnet build src/Axial.Guardrails/Axial.Guardrails.fsproj -c Release -v q -nologo`: passed, 0 warnings/errors
  (`artifacts/verify/internal-phase3-analyzer-build.log`).
- Focused Release tests, `FullyQualifiedName~ConcurrencyHazards`, with `-p:BuildProjectReferences=false`:
  **67 passed** (`internal-phase3-tests.log`). The prior run was real RED on the new Task-cast positive witness:
  **1 failed, 66 passed** (`internal-phase3-tests-red.log`); following erased cast ranges fixed it.
- Full Release Guardrails suite using already compiled references: **174 passed**, no failures/skips
  (`internal-phase3-release-tests.log`). Fixture parse/type sanity assertions passed too.
- Read-only old/new CLI witness snapshots: 8 findings before, 1 after; the remaining unclassified fallback is
  intentional and proves the guard exemption does not cover the next handler. Script scanning alone does not
  validate typed-tree examples; the xUnit suite does.
- `git diff --check`, `scripts/check-docs-conventions.sh`, `scripts/check-source-inventory.sh`: passed.
- `axial check`: correctly blocked for the 10 runtime entries and 84 allowances; no baseline additions or rule
  downgrades (`internal-phase3-policy-check.log`).
- No phase-3 `artifacts/verify/result.json`, full axial-verify or done-check pass. Full solution build/verification
  is approval-dependent. The torture waiver is local `artifacts/verify/torture-waiver.txt` (comments-only changes).
- Narrowing-specific fail-first against pre-phase source `be0e59fa9`: passed (script exit 0), with actual AXG010
  and AXG012 assertion failures. `internal-phase3-fail-first.txt` records implementation checkpoint `1810f3415`;
  `.exit` records success. This is not a policy-lock or compilation failure. The final-HEAD origin/main proof is
  still required in phase 4.

## Important integration observations

- Http.fs:556 contains a quote character literal. The current suppression lexer cannot see an inline directive
  after it, so its AXG010 allowance is on the preceding if line. The repeated HttpClient scan then exited 0.
- ProcessLive.fs:144 is a netstandard-only Task.Run native wait. Its unbounded pool-worker occupancy needs review;
  an allowance in the inactive branch would also be stale under AXG002 on net8. It remains suspicious/proposed
  baseline, not an unsafe allowance. No inactive-code suppression semantics changed.
- Post-allowance scans with the old analyzer printed 74 findings in the initial target selection, plus the four
  unchanged net8-only TimeProvider findings = 78, matching 162 - 84. No AXG002 referenced a new allowance.
  The partial scanner config enables only the Axial rule group, so existing AXG001 operational allowances can
  appear stale; those diagnostics trigger the CLI SARIF writer's empty-URI error. Use printed findings in those
  post-scan logs, not absent SARIF, and do not call that a full suppression-integrity pass.

## Exact next step

1. Phase 3 is complete at the approval boundary. STOP; do not generate or approve policy/baseline changes.
2. Coordinator/human reviews the report table, 10 runtime entries, 84 specific allowance reasons and 63 suspicious
   baseline candidates (AXG009 36 / AXG010 17 / AXG011 0 / AXG012 10). They own baseline/approval commands and
   combining this batch with any other lane's policy changes. Do not baseline all findings indiscriminately.
3. Once explicitly authorized/reconciled: normal solution build, full `axial-verify`, final HEAD-matching fail-first,
   clean HEAD-matching `artifacts/verify/result.json`, and done-check in phase 4. Include the HTTP adapters in any
   source inventory; they are not solution build coverage. Keep this handover until its useful content is retained
   elsewhere. No push unless separately requested.
