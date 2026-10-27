# AXG013 — phase 2 handover

## State

Phase 2 implements AXG013 and docs. No dotnet command was run in phase 2, per the coordinator's lock decision.
No merge, push, baseline edit, baseline generation, or approval command was performed.
Worktree: `/home/adam/projects/Axial/feature/guardrails-test-waits`.
Brief: `/home/adam/projects/scratch/axial-coord/pi/guardrails-test-waits.md` (read its Coordinator decision and TOKEN-RULES.md).

## Implementation and decisions

- `WaitForFactsAnalyzer.analyzeWithConfig config ctx` resolves effective policy role, runs only in `test`, and
  uses rule key `test.wait-for-facts`, default deny. Explicit allow/warn are honored; deny/forbid produce errors.
- Symbol identity distinguishes BCL Thread.Sleep, Task.Delay and FSharpAsync.Sleep from lookalikes.
  Thread.Sleep calls are reported; Task.Delay/Async.Sleep are reported only as direct `do!`/`let!` sources.
  Parenthesized/typed direct sources are recognized. No helper/alias data flow analysis is claimed.
- Deadline arms are naturally excluded: awaiting WhenAny is not directly awaiting Delay. A separately bound
  delay is not itself awaited. Fact waits and condition polling are not targets.
- Flow.sleep / Clock.sleep deliberately remain out of scope: syntax cannot determine the supplied clock.
- Shared shrink-only baseline application is retained, as are boundary-file/site suppression checks. AXG002
  recognizes AXG013 and uses its raw ranges to reject stale directives.
- Catalog uses an inert structural-rule entry, as AXG008 does. Both analyzer and CLI policy allowlists register
  the new policy key. Registration changes stay small because another lane owns AXG009–AXG012.
- Corrected two phase-1 fixture setup issues without relaxing expected behavior: temporary policy now declares
  the fixture boundary file, and the site-locality test puts a blank line after the first suppressed site because
  existing suppression machinery intentionally covers the immediately following line too (expected line now 9).

## Files touched in phase 2

- `src/Axial.Guardrails/WaitForFactsAnalyzer.fs` (new).
- `src/Axial.Guardrails/Axial.Guardrails.fsproj`, `EffectCatalog.fs`, `GuardrailConfig.fs`,
  `SuppressionIntegrityAnalyzer.fs`.
- `src/Axial.Tool/PolicyLock.fs`.
- `tests/Axial.Guardrails.Tests/WaitForFactsAnalyzerTests.fs` (fixture corrections above).
- `docs/15-notes/03-guardrails.md`, `docs/15-notes/04-guardrail-diagnostics.md` (index, scope, reported/fixed examples).
- `dev-docs/releases/0.12.0.md`, this handover.

## Verification / fail-first

FAIL-FIRST AND GREEN TESTS PENDING. No phase-2 compilation, test, livedocs execution or verify run.
No phase-2 `artifacts/verify/result.json` exists. Only static diff checks are available; do not treat this as passing.

Phase 1's normal run stopped at policy check MSB3073 before tests. A pre-clarification bypass run failed FS0039
for missing WaitForFactsAnalyzer, but is NOT accepted proof and must not be repeated. That run also exposed a
Policy.Config assembly-local type mismatch, already fixed by parsing policy via GuardrailConfig.
Phase-1 logs remain under `/home/adam/projects/scratch/axial-coord/pi/`:
`guardrails-test-waits-phase1-test.log`, `guardrails-test-waits-phase1-red.log`, `guardrails-test-waits-policy.log`.

## Exact next step

STOP after phase 2 commit. Wait for coordinator instruction after the lock-fix lane lands and human lock approval.
Only then merge origin/main as instructed and run focused tests WITHOUT bypassing policy. Inspect compiler
syntax/type errors in every source fixture, not merely analyzer results; allowed fixtures must typecheck too.
Check FCS symbol identity for Async.Sleep and all awaited forms. Preserve behavioral expectations.
Fail-first evidence still needs an approved run against unfixed source; implementation existence alone is not proof.

Phase 3: classify existing violations and report per-file counts without rewriting repository tests. STOP before
`axial policy baseline --rule AXG013`: baseline generation is a HUMAN DECISION. Full verify, fail-first artifact and
done-check belong to phase 4, after approval. Never bypass the lock check or report full completion prematurely.

## Open questions / risks for execution phase

- All implementation and documentation examples are uncompiled under the explicit no-dotnet instruction.
- Existing fixtures may have F# indentation/type diagnostics; validate them when permitted.
- Baseline counts and approval are not prepared yet (phase 3); no existing test violations were rewritten.
