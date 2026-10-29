# Guardrails review A — phase 1 blocked on legacy comparison semantics

Worktree: `/home/adam/projects/Axial/chore/guardrails-review`. No push/merge. No protected files changed; no approval/baseline command executed.

## Actual design conflict

The required first reference `v0.11.3` contains **none** of `Axial.Guardrails.toml`, `Axial.Guardrails.lock`, or `Axial.Guardrails.baseline`. Confirm with:

```sh
git ls-tree --name-only v0.11.3 Axial.Guardrails.toml Axial.Guardrails.lock Axial.Guardrails.baseline
```

The design specifies a fallback when a historical lock lacks suppression inventory, but does not define historical role/rule/runtime/boundary state when TOML itself does not exist. Historical behavior was encoded in build configuration and analyzer implementation. Inventing an empty/default v2 policy could falsely call tightening a relaxation or miss legacy exceptions, contrary to the requirement to show only loosening and the no-behavior-changing-fallback instruction.

Need owner direction: define the pre-TOML comparison semantics (for example explicitly report v2 policy introduction separately while comparing source exceptions and evaluated legacy settings), or require legacy-policy reconstruction. Do not silently substitute today's v2 defaults.

## Work completed

- Tests committed before fixes; valid original red run: 21 failures, 2 passes, `artifacts/verify/phase1-red.log`.
- Implemented warn-only check, CLI review/JSON/check/since/default reviewed point, commit metadata on approval (without executing approval), help wording.
- Review scans historical and working source comments with one lexer, compares policy inventories, evaluates project settings through MSBuild including each TFM, and reports automation/package changes.
- Follow-up edge-case tests were run red before fixes (`artifacts/verify/phase1-extra-red.log`).
- Focused suite before adding the legacy blocker reproducer: 57 passed, 0 failed (`artifacts/verify/phase1-green.log`).
- New `review supports the required pre TOML starting tag` test intentionally remains red, reproducing the real required-tag failure (`artifacts/verify/legacy-ref-red.log`).
- Real repository reproduction: `dotnet --roll-forward Major artifacts/bin/Axial.Tool/debug/Axial.Tool.dll policy review --since v0.11.3 --json` exits 1 because historical TOML does not exist (`artifacts/verify/repository-review.json`).

## Not done

Full verification, final fail-first script, done-check, phase 2 docs/release automation, and final review are not done. Do not treat this as PHASE 1 DONE.

The installed SDK is .NET 10 only; invoking net8 tool directly needs `--roll-forward Major` (tests supply their net10 runtimeconfig). Verification wrapper is `/home/adam/projects/scratch/axial-coord/limits/bin/axial-verify`, not initially on PATH.
