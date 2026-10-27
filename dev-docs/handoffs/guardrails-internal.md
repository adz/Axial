# Guardrails internal — phase 2 handover

## State and coordinator override

Phase 2 implementation and docs written WITHOUT running dotnet, builds, tests, or verification.
Compilation and focused green tests are NOT established. Fail-first proof remains pending.
The coordinator decision at the end of `/home/adam/projects/scratch/axial-coord/pi/guardrails-internal.md`
explicitly supersedes phase 1's instruction to wait before implementing.

Tests must remain unrun until the coordinator says the lock fix is on main and instructs this lane to merge
`origin/main`. Do not bypass `axial check`, edit the lock, approve policy, or generate/approve baselines.
Phase 1 tests: `f3be1dbc2`; checkpoint before phase 1: `acca6316e`; previous handover: `0fd7a3f1c`.
No push. No runtime behavior changes, per-site allowances, or baseline changes.

## Implementation / decisions

- `src/Axial.Guardrails/ConcurrencyHazardsAnalyzer.fs`: four registered CLI analyzers, AXG009–AXG012.
  Symbol identity detects raw primitives, CancellationToken.None, and Async.Start/StartImmediate.
  Syntax detects unhandled catch clauses and local discards; typed expression ranges detect Task/ValueTask/Fiber
  discards (including abbreviations and aliased task factories). Direct discarded fork flows are also detected.
- All four exempt the resolved `test` role; every other role gets errors. With no policy they run by default.
- AXG010 treats bound exception use as recording; accepts raise/reraise and call names containing Report, notify,
  Observer, Log, Record, case-insensitive. This is intentionally heuristic, not path-sensitive proof.
- All four allow reasoned per-site comments without boundary membership. AXG002 rejects file-wide allowances,
  absent reasons, and stale directives, using the new rules' raw ranges. Prior rules retain their existing behavior.
- `Policy.Config.Runtime` parses `[runtime].files`, validates existing in-root paths, and rejects globs.
  Runtime exemptions affect AXG009 only; adapter boundary membership does not exempt it.
- `Axial.Guardrails.toml` proposes only App/Cache/Core/Flow/Hub/Parallelism/Platform/Queue/Ref/Stream runtime files.
  No adapters are included. This policy change is deliberately UNAPPROVED.
- `PolicyLock.inspect` tracks runtime exemptions in its existing approval-sensitive boundary set with a
  `runtime:` prefix (also reflected in boundary-change summaries); the policy file hash protects all edits.
- Catalog category entries, fsproj compilation registration, diagnostic reference sections, and 0.12.0 migration
  notes updated. No AXG013 changes; that belongs to another lane.

## Tests / validation status

Existing phase 1 test file covers primitive forms, handlers, discarded work, adjacent suppression, malformed reasons,
roles, runtime exemption, and boundary non-exemption. Its `(site: string)` inference annotation was already dirty on
session entry and is retained in this phase's commit.
Added a policy-lock test proving runtime list growth appears as an approval-sensitive boundary change and policy hash
change. That test is also UNRUN. No fail-first claim can be made for it.

No dotnet command was executed in phase 2. Only static inspection and `git diff --check` were used.
`artifacts/verify/result.json` does not validate this work. Full verify and done-check remain pending.

## Exact next steps

1. Stop at this phase boundary. Wait for coordinator's explicit lock-fix-on-main notification.
2. When instructed, merge `origin/main` (never checkout/switch or bypass the lock); resolve any AXG013 integration
   in suppression registration / catalog / project compilation while retaining both lanes.
3. Establish fail-first evidence against the pre-implementation analyzer using the approved procedure. Tests have
   not yet even compiled. Fix fixture/compiler issues and record actual assertion failures, not lock failures.
4. Run focused tests:
   `dotnet test tests/Axial.Guardrails.Tests --nologo -v q --filter 'FullyQualifiedName~ConcurrencyHazards|FullyQualifiedName~PolicyLockTests'`
   Address compiler/behavior failures; do not infer green from this handover.
5. Phase 3: classify EVERY src finding in the external report, with file:line/rule/class/reason. Add only specific,
   true legitimate per-site reasons. Suspicious/unjustified sites remain unfixed and need proposed baseline counts.
   STOP for human approval before any `axial policy baseline` / `axial policy approve` command.
6. Phase 4: approval-dependent full verification, final fail-first artifact, clean HEAD-matching result.json,
   done-check. Do not claim final completion before those pass.

## Remaining review points

- Static implementation only: confirm FCS symbol names and typed-range correspondence against executable tests,
  especially F# lock and implicit task statements. Tighten Fiber statement/ignore witnesses if necessary.
- Syntax traversal shares ResourceFromFiberAnalyzer's walker; it is a local heuristic, not whole-program analysis.
- Runtime entries are exact paths, not pattern expansion. Lock wire format reuses BOUNDARY entries with `runtime:`
  prefix rather than adding a new snapshot field; no lock was regenerated.
- User docs show diagnostic examples without claiming observable output. FsLiveDocs validation is pending.

External report: `/home/adam/projects/scratch/axial-coord/pi/guardrails-internal.report.md`.
