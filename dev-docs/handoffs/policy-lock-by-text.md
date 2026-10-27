# Policy lock by text — phase 1

## Scope and state

Brief: `/home/adam/projects/scratch/axial-coord/pi/policy-lock-by-text.md`.
Branch/worktree: `fix/policy-lock-by-text`, `/home/adam/projects/Axial/fix/policy-lock-by-text`.
Phase 1 adds tests only; production code and the repository lock are untouched. Stop after this phase.

## Decisions and evidence

- Tests exercise `inspect`, serialization/parser round-trip, and `diff` against temporary repositories, rather than constructing the proposed new identity format.
- Require line insertion/removal to yield an empty diff. Duplicate test moves the original while adding another identical comment, requiring exactly one addition.
- Rule/reason changes and cross-file movement assert removal of the old identity and one added identity.
- Migration choice: accept legacy version-1 line-numbered records but require review/reapproval, even when source lines still match. The legacy fixture pins the version header and old identity explicitly.
- Brief invariant 6 conflicts with existing behavior: removal produces no `SuppressionsAdded`, but does produce `ProtectedChanged = [LockName]`, so `isEmpty` is false and check blocks. The characterization test preserves that behavior and verifies ordinary approval accepts the removal. Do not silently make removals pass; coordinator should resolve if a behavior change is desired.
- No lock approval has been run or authorized.

## Files touched

- `tests/Axial.Guardrails.Tests/PolicyLockTests.fs`: fixtures and tests for all seven brief invariants.
- This handover.

## Exact next step (fresh session, phase 2)

Read the brief and this handover, then implement text-based occurrence-aware suppression identity in `src/Axial.Tool/PolicyLock.fs`. Keep legacy parsing readable but untrusted pending reapproval. Update relevant format docs (search `docs/` and `dev-docs/` for `Guardrails.lock`, reading `dev-docs/DOCS.md` first) and `dev-docs/releases/0.12.0.md`. Run focused PolicyLockTests until passing. Do not refresh `Axial.Guardrails.lock`. Update/commit this handover with phase 2 and STOP.

## Verification

Committed tests were run against unchanged production code: 17 total, 13 passed, 4 failed (both line-movement cases, duplicate-plus-movement, legacy reapproval). Command: `dotnet test tests/Axial.Guardrails.Tests/Axial.Guardrails.Tests.fsproj -c Release -p:_AxialRepoSkipGuardrails=true --filter FullyQualifiedName~PolicyLockTests --nologo -v q`. Exit 1 reflects assertion failures, not compilation failure. Log: `/home/adam/projects/scratch/axial-coord/pi/policy-lock-text-phase1-tests.log`. The external lane report also records this result. Full verification/result.json and done-check are deferred to phase 4, per the brief. Passing characterization tests are expected; the regression suite must be red against unchanged production code.
