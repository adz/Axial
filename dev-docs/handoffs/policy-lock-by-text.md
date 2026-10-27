# Policy lock by text — phase 2c complete

## Scope and state

Brief: `/home/adam/projects/scratch/axial-coord/pi/policy-lock-by-text.md` (including coordinator additions).
Branch/worktree: `fix/policy-lock-by-text`, `/home/adam/projects/Axial/fix/policy-lock-by-text`.
Phase 2 implementation and docs are complete; focused PolicyLockTests pass. No push or lock approval was run.
Repository `Axial.Guardrails.lock` is untouched. Stop after this phase.

## Decisions

- FIRST removed mandatory hashing of `.fs`/`.fsproj` under `tests/Axial.Guardrails*`, with a separate implementation commit (`62bfd0aa1`) before suppression identity work. Explicit protected globs still apply. Enforcement config remains protected.
- Suppressions use file + normalized comment text + occurrence number, stored as a set of occurrence identities. Normalization extracts the suppression comment, canonicalizes its prefix, trims outer reason whitespace, and preserves internal reason text. Line movement is ignored; duplicate additions, changed rules/reasons, and cross-file movement remain additions.
- Writer emits format version 2. `SUPPRESSION` records contain tab-separated file, occurrence number, comment. `BASELINE_PATH` records the configured baseline file. Snapshot carries Version and BaselinePath for comparison.
- Version-1 locks remain parseable but always produce a lock-review change, even without suppressions. A human must review and reapprove once. No automatic migration/trust.
- Coordinator resolved invariant 6: suppression removal and baseline shrinkage strengthen policy and pass without approval. Supersedes phase-1 characterization. Baseline hash differences are waived only for the same baseline path when parsed entries changed with no additions/count growth; other protected-file changes still block. Next approval refreshes the snapshot.

## Files touched

- `src/Axial.Tool/PolicyLock.fs`
- `tests/Axial.Guardrails.Tests/PolicyLockTests.fs`
- `docs/15-notes/03-guardrails.md`
- `dev-docs/current-ideas/guardrails-protected-files.md`
- `dev-docs/releases/0.12.0.md`
- This handover.

## Test-first evidence

- Commit `a52597fd5`: protected-input regression test committed first, then failed 1/1 against old hashing. Log: `/home/adam/projects/scratch/axial-coord/pi/policy-lock-phase2-protected-red.log`. After first implementation commit, passed 1/1.
- Commit `35db864ea`: updated suppression-removal test and added baseline-shrink regression before implementation. Full PolicyLockTests run failed 6, passed 12 (line movement twice, duplicate-plus-movement, legacy review, suppression removal, baseline shrinkage). Log: `/home/adam/projects/scratch/axial-coord/pi/policy-lock-phase2-red.log`.
- After implementation, focused tests passed 18/18. Log: `/home/adam/projects/scratch/axial-coord/pi/policy-lock-phase2-green.log`.
- Command: `dotnet test tests/Axial.Guardrails.Tests/Axial.Guardrails.Tests.fsproj -c Release -p:_AxialRepoSkipGuardrails=true --filter FullyQualifiedName~PolicyLockTests --nologo -v q`.
- `bash scripts/check-docs-conventions.sh` passed; `git diff --check` passed.
- Full verification, livedocs execution, final fail-first artifact, result.json, and done-check remain phase 4 work. No current phase-2 result.json is claimed.

## Phase 2b — skipped-project fixture text

- Coordinator merged origin/main before this session. Added a four-case regression theory in commit
  `b63ce0c0c` before changing implementation. All four cases failed against the unfixed code.
- `PolicyLock.suppressions` now excludes `tests/Axial.Guardrails*/`, `tests/Axial.PrintfFixture/`, and
  `benchmarks/Axial.Comparative/`. The test checks that adding fixture-string suppression text in each directory
  leaves the diff empty, then adding the same text under `src/` adds exactly one suppression.
- The exclusion list stays local to PolicyLock with a pointer to Directory.Build.props. Evaluating MSBuild project
  conditions inside the standalone policy scanner would introduce disproportionate complexity; these directory
  exclusions implement the coordinator's specified scope (including all Guardrails fixture projects).
- Focused PolicyLockTests: 22 passed, 0 failed. Same command as phase 2 above. Logs:
  `/home/adam/projects/scratch/axial-coord/pi/policy-lock-phase2b-red.log` (4 failed),
  `/home/adam/projects/scratch/axial-coord/pi/policy-lock-phase2b-green.log` (22 passed).
- Updated the 0.12.0 Changed notes. No lock approval, lock edits, or push. Full verify/result.json and done-check
  remain phase 4 work; this phase does not claim full verification.

## Phase 2c — policy-configured suppression exclusions

- Replaced hardcoded repository fixture paths with optional `[suppressions].ignore` globs parsed by
  `src/Axial.Guardrails/Policy.fs` into `Config.SuppressionIgnores`. The scanner uses the policy glob matcher;
  absent configuration means no exclusions. Patterns are canonical repository-relative paths, may match nothing,
  and receive the same outside-root validation as other policy globs.
- Moved this repository's three fixture patterns into protected `Axial.Guardrails.toml`. No lock approval or lock
  modification was performed. Changing this list requires human approval because the TOML is hashed.
- Updated the TOML reference in `docs/15-notes/03-guardrails.md` and the 0.12 Changed notes.
- Test-first commit: `aa0bc0e1e` (`test: require policy-configured suppression scan exclusions`).
  Before implementation, focused tests failed 5, passed 18: four configured-ignore cases rejected the new setting,
  and the unconfigured fixture case exposed the hardcoded exclusion. Red log:
  `/home/adam/projects/scratch/axial-coord/pi/policy-lock-phase2c-red.log`.
- After implementation, PolicyLockTests plus PolicyTests passed 36/36. Green log:
  `/home/adam/projects/scratch/axial-coord/pi/policy-lock-phase2c-green.log`.
  Command: `dotnet test tests/Axial.Guardrails.Tests/Axial.Guardrails.Tests.fsproj -c Release -p:_AxialRepoSkipGuardrails=true --filter 'FullyQualifiedName~PolicyLockTests|FullyQualifiedName~PolicyTests' --nologo -v q`.
- Docs conventions and whitespace checks passed. Full verification/result.json, livedocs, final fail-first artifact,
  and done-check remain phase 4 work; no full-verification claim is made for this phase.
- This supersedes phase 2b's decision to keep a local hardcoded exclusion list.

## Exact next step (fresh session, phase 3)

Read the brief and this handover. Perform classification/approval preparation only. The repository lock is still version 1 and will require human approval under the new reader; hashes also still include formerly mandatory test files. Do NOT run `axial policy approve` or edit the lock. If checking confirms approval is the only blocker, report it to the coordinator and stop per the brief. Update/commit this handover and reply PHASE 3 DONE. Phase 4 owns full verification and done-check after coordination of approval.

## Open questions / constraints

No unresolved design questions. Human lock approval remains outstanding. Do not mistake focused tests using `_AxialRepoSkipGuardrails=true` for a full verified repository build; that override only isolates these policy tests from the intentionally unapproved repository lock.
