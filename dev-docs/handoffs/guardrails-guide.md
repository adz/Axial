# Guardrails guide — phase 1 handoff

STOP: phase 1 only. Fresh coordinator context required before phase 2.

## Context and checkpoints

- Worktree: `/home/adam/projects/Axial/feature/guardrails-guide`, branch `feature/guardrails-guide`.
- Starting clean checkpoint: `f76d8476c`.
- Merged internal tip `701d4cc2d` in `354b71a63`; test-waits tip `fe7107b0a` in `f019f24ff`.
- Reconciled release notes, rule catalog, AXG002 dispatch/known ids, and policy-lock tests, retaining both branches and main's tests.
- Removed imported AXG009–013 source allowance comments and restored TOML to checkpoint (no runtime section).
- Kept analyzers/tests/targets and runtime-policy parser support. Quoted allowance syntax in test fixtures remains intentional test input.
- Existing AXG001–008 allowances, baseline and lock untouched. Operational source restored to checkpoint content.

## Proposal awaiting coordinator decision

Full report: `/home/adam/projects/scratch/axial-coord/pi/guardrails-guide.report.md`.

- AXG009–013 are advice warnings; no development-build failure, including warning-as-error paths.
- `axial-note AXGnnn: reason` attaches on the same/next line, keeps findings visible, has no file/boundary/lock exemption.
- AXG002 validates notes separately at warning severity; invalid/stale notes cannot satisfy a checkpoint. Legacy allowance validation stays unchanged.
- Shared effective finding collector feeds atomic project/TFM snapshots and `axial review`; `--check` gates Release/verify on unacknowledged findings/invalid notes. Incomplete or failed inventory is a tool failure, never zero findings.
- Generated reports list every finding, acknowledgement and provenance; reject stale/missing snapshots.
- Preserve AXG001–008 error/allowance/lock contract for now. New notes never enter suppression-lock identities. Broader legacy migration needs a separate decision.

## Lock blocker / next action

Imported AXG013 package targets and analyzer tests/project files touch protected inputs. Source lane already reports policy-check blockage. No approve/baseline/bypass command was run; exact current diff was not evaluated. Coordinator/human must resolve/authorize the lock transition before later implementation/verification. Do not self-approve.

Phase 2, only after coordinator authorization: narrowing tests first. AXG013 already has helper-only polling exemption and default test wiring, but inspect infinite waits (currently even Thread.Sleep infinity can qualify), uniquely approved helper identity, and bounded SpinWait coverage. Do not treat inherited lane test evidence as verification of this merge.

## Validation and stopping boundary

Quiet-machine benchmark: **no dotnet command run**. No build/test/verify/done-check; no new result.json or fail-first evidence claimed. `git diff --check` passed. Commits local only; no push. This is phase completion, not completion of the implementation slice. Do not continue phases 2–5 in this session.
