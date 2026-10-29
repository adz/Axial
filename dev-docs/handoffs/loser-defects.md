# Loser-defects handover

Blocked on contract clarification; see `/home/adam/projects/scratch/axial-coord/pi/loser-defects.report.md`.

Unchanged runtime: raw internal Execution task faults from race/timeout losers are silently dropped. New `FiberObserverTests.Escaped loser exception is reported once without replacing winner` theory proves both cases fail. Actual public `Flow.fromTask` catches foreign exceptions into Cause.Die: timeout reports once, race returns the loser defect to its caller (existing contract), rather than preserving a successful winner. New `Task interop loser exceptions follow the Cause Die contract` theory proves both cases pass.

Need coordinator confirmation whether only raw internal task faults should change, preserving public Cause.Die race semantics. Brief's requirement of a fromTask exception that is not an Exit failure cannot occur through that adapter.

Tests committed before any source change. No runtime edits, no push/merge, no policy approval or protected-file edits. Last filtered run: 2 passed, 2 intentionally failed; persistent logs in coordinator pi directory. Full verify/done-check not run because unresolved behavior and red tests. No completion claimed.

After clarification: fix the two raw fault paths through the existing observer channel without double-reporting, add torture invariant (or justified waiver), before/after Race and FiberCost benchmarks, release sentence if behavior changes, 20x touched class, final fail-first proof, clean final commit, detached axial-verify, then done-check. Wrapper is `/home/adam/projects/scratch/axial-coord/limits/bin/axial-verify` (not initially on PATH).
