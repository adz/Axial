# Guardrails review A — phase 1

Worktree: `/home/adam/projects/Axial/chore/guardrails-review`. Commit locally; no push/merge. Protected files remain untouched. No approval/baseline command should be executed.

## Legacy comparison decision (owner)

The required reference `v0.11.3` contains no TOML, lock, or baseline. Do not reconstruct historical analyzer policy. Instead, emit a first `Policy introduced since <ref>` section explaining that policy files did not exist and summarizing current roles/project counts, non-error rule levels, runtime/exemption/boundary/ignore/baseline counts. Introduction counts as one loosening item for `--check`. Compare source suppressions/notes at that ref with the working tree using the same lexer. Automation/package changes still have concrete history and are compared independently.

Implemented and covered by `review supports the required pre TOML starting tag`. Real `v0.11.3` review also succeeds.

## Implementation

- `axial check` emits one warning with a change count and review instruction for lock drift, exiting zero; malformed lock/config still fails.
- `policy review [--since <ref>] [--json] [--check]` compares repository state, inventories exceptions and policy changes, and evaluates project properties/items with MSBuild for every target framework.
- Missing reviewed point requires explicit `--since`; lock supports a validated `REVIEWED_POINT` SHA.
- Approval prepares the lock with the current commit even if policy itself did not change. The metadata helper is tested without executing the command. `--yes-i-reviewed` remains; help describes approval as human review, not authority.
- Consumer-build tests and the migration regression now assert warn-only drift while analyzer errors remain errors. Consumer fixtures construct their temporary locks directly rather than invoking approval commands.
- The first full verification exposed a pre-existing 20 ms race in the fiber-metadata test. It passed unchanged on the required full retry; the fixture was then made deterministic with a release gate and recorded in `VERIFICATION.md`, rather than rerunning past it. No runtime/concurrency implementation changed.

## Proof and verification

Original behavioral red: 21 failed, 2 passed (`artifacts/verify/phase1-red.log`). Extra edge-case red runs cover defaults, removed enforcement, conditional per-TFM settings, notes/file-wide exceptions, and legacy introduction. The approval-text helper test initially failed compilation before implementation.

Focused policy suite: 61 passed, 0 failed (`artifacts/verify/phase1-green.log`). Consumer policy/cache integration slice: 4 passed (`artifacts/verify/package-policy.log`). The migration regression temporarily appends/restores a TOML comment as part of its drift test; this is the only worktree TOML mutation, with no lasting TOML change. Final verification status belongs in `artifacts/verify/result.json` and the persistent report `/home/adam/projects/scratch/axial-coord/pi/guardrails-review-A.report.md`; check those rather than assuming this handoff certifies completion.

Phase 2 docs/release automation, its 20x repetitions, and release notes remain explicitly deferred to phase 2. A human must approve the repository lock after merge; agents must not edit it.

SDK here is .NET 10 only; invoking the net8 tool directly needs `dotnet --roll-forward Major <tool.dll>` (tests supply their net10 runtimeconfig). Verification wrapper: `/home/adam/projects/scratch/axial-coord/limits/bin/axial-verify`.
