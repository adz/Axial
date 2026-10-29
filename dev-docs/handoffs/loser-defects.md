# Loser-defects handover

Coordinator clarified scope: fix internal raw Execution task faults only; preserve public interop and Cause.Die race semantics.

## Implementation

- Platform timeout converts an escaped losing task fault into a discarded Die exit, using the existing safe observer path.
- Platform race distinguishes a settled Exit from an escaped raw-task fault via a struct Result callback; Flow reports the latter as unobserved and leaves loserExit unset, so it cannot replace the selected winner.
- Canceled raw tasks retain the previous interruption behavior and are not observer defects. Actual Exit/Cause.Die loser settlement is unchanged.
- Obsolete AXG010 notes were removed because the handlers no longer swallow arbitrary faults; narrow cancellation handling needs no suppression. No new policy suppressions, lock changes, or approval commands.

## Evidence

FiberObserverTests adds raw-task fault laws for timeout and race, each repeated with a sink that records then throws. Assertions check unchanged result, exception identity, and exactly one event. Public Flow.fromTask characterization confirms its exceptions become Cause.Die (timeout reports once, race returns the consumed defect); existing tests are unchanged. Raw-task cancellation tests pin no defect event.

Initial raw-task law cases fail on the original runtime. A first implementation also failed both cancellation laws before narrowing cancellation handling. Final class: 21 tests pass, repeated 20/20 runs. Persistent logs: `/home/adam/projects/scratch/axial-coord/pi/loser-*.log` and `loser-repeat.summary`.

Torture waiver: `artifacts/verify/torture-waiver.txt`; the consumer-only torture assembly cannot construct raw internal tasks and public interop cannot reach these branches. No internal access, reflection, or public API was added to work around that boundary.

Before/final-after benchmarks filter the complete FiberCost class plus ParallelCompositionBenchmarks.Race; raw outputs are under `artifacts/benchmarks/loser-before` and `loser-after-final`. Final measurements and verification outcome are recorded in the persistent report below.

Final fail-first proof: `artifacts/verify/fail-first.txt`. Full detached axial-verify: `artifacts/verify/result.json`. Final coordinator check: `/home/adam/projects/scratch/axial-coord/pi/loser-done-check.log`.

Persistent final report: `/home/adam/projects/scratch/axial-coord/pi/loser-defects.report.md`. No push or merge.
