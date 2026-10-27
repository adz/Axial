# Guardrails internal — phase 1 handover

## State

Phase 1 tests committed; test execution BLOCKED by the existing policy lock. No production implementation, docs, runtime changes, suppressions, baseline changes, or approval commands. Do not start phase 2 until the tests have actually executed against the unchanged analyzer and shown assertion failures.

Brief: `/home/adam/projects/scratch/axial-coord/pi/guardrails-internal.md`.
Token rules: `/home/adam/projects/scratch/axial-coord/pi/TOKEN-RULES.md`.
Checkpoint before work: `acca6316e`. Test commit: `f3be1dbc2` (followed by this handover commit).

## Files

- `tests/Axial.Guardrails.Tests/ConcurrencyHazardsAnalyzerTests.fs`: new behavioral tests.
- `tests/Axial.Guardrails.Tests/Axial.Guardrails.Tests.fsproj`: compile registration.
- This handover.
- External report: `/home/adam/projects/scratch/axial-coord/pi/guardrails-internal.report.md`.

## Test design / decisions

- Discover the assembly's exported static `Analyzer<CliContext>` properties and invoke their CLI delegates. This uses existing entry points, needs no placeholder production API, and should fail assertions rather than simply failing to compile because new APIs do not exist. Missing registration must leave positive tests red.
- AXG009 covers every listed primitive; AXG010 covers swallowed wildcard, named and typed handlers, fallback values, propagation, recording by returning the exception, and all five named reporting conventions; AXG011 covers None; AXG012 covers Flow.fork, wildcard task/value-task/fiber bindings, task statements, ignored tasks/value tasks, aliases and Async.Start/StartImmediate.
- Adjacent suppression tests require exactly one of two sites to remain. AXG002 must identify missing reasons (not merely reject an unknown ID).
- All four rules exempt the test policy role, consistently; all other roles are exercised.
- Proposed policy extension is `[runtime] files = [...]`. A fixture uses a generic runtime file, not this repository's concrete runtime allowlist. Runtime exemption must affect only AXG009; boundary files must remain subject to AXG009.
- Policy environment fixtures use an xUnit collection with DisableParallelization=true and restore the environment in finally, so they do not contaminate other tests.
- Negative tests are expected to pass on the old analyzer; positive tests must fail. Exact deduplication for every primitive is not prescribed, but suppression cases and simple cases require a single diagnostic.

## Attempt / blocker

Committed tests first, then ran:

```sh
dotnet test tests/Axial.Guardrails.Tests --nologo -v q --filter 'FullyQualifiedName~ConcurrencyHazards'
```

The wrapper waited for the shared dotnet lock normally. The run then exited 1 before tests executed:

`src/Axial.Guardrails/build/Axial.Guardrails.targets(103,5): error MSB3073: ... Axial.Tool.dll check ... exited with code 1` for `src/Axial/Axial.fsproj::TargetFramework=net8.0`.

Guardrails test sources/project files are protected policy-lock inputs (targets line 64). Changing them invalidates the checked-in lock. This is NOT fail-first behavioral proof, and test compilation has not yet been established. Do not claim otherwise. Full log is persistent at `/home/adam/projects/scratch/axial-coord/pi/guardrails-internal-phase1-tests.log`.

No `axial policy approve`, `axial policy baseline`, lock edits, or analyzer-disable switches were used. No full verification or done-check was run for this intentionally red phase; `artifacts/verify/result.json` does not represent this work.

## Exact next step

Coordinator/human must resolve how to authorize protected test-input changes without prematurely approving later baseline/policy growth. Obtain that decision first. Then rerun the exact focused test command above, correct any fixture/compiler problems, and record actual assertion failures against unchanged production code. Commit any test corrections with updated handover before proceeding to implementation in a fresh session.

## Remaining phase-1 coverage to review once executable

- Runtime-list lock protection itself is not yet tested through the policy tool; implementation must extend existing PolicyLockTests to prove runtime-list drift needs approval, as boundaries do.
- A direct Fiber-typed statement/ignore witness and ordinary misleading-name counterexamples could tighten typed AXG012 coverage beyond the current Fiber wildcard binding and aliased Task factory.
- The fixture helper does not explicitly assert FCS diagnostics are error-free; establish that the positive witnesses typecheck, especially implicit discarded Task statements.

## Later phases

Phase 2: implement/register AXG009–AXG012 (leave AXG013 to the other lane), runtime policy parsing/config propagation/lock protection, suppression-integrity coverage, and diagnostic docs; focused tests green. Read dev-docs/DOCS.md before user docs.
Phase 3: classify every src finding in the external report, justify legitimate per-site allowances, prepare suspicious/unjustified baseline counts, STOP before approval/baseline commands.
Phase 4: approval-dependent full verification and done-check, final fail-first proof. No push.
