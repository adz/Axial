# Cleanup helper

ProcessRun.bestEffort centralizes ten cleanup catches; the result-selecting isLive catch remains separate. A static generic helper takes explicit state and noncapturing actions, avoiding per-call closures; a unit test measures zero bytes across 10,000 successful calls. Inline exception handling failed F# Release cross-assembly inlining, so the final implementation deliberately does not use inline. OutOfMemoryException, StackOverflowException and AccessViolationException propagate (synthetic OOM tested); ordinary exceptions remain tolerated with unchanged disposal grouping/order.

ProcessServiceTests characterization cases passed on old code (82 tests). Helper tests were committed first and failed compilation without the helper; final class has 85 tests and passed 20 consecutive runs. User explicitly authorized passing-before-and-after characterization tests as a fail-first waiver. Artifacts record the waiver and separate helper red proof. No public API change; no benchmark required because the helper uses explicit state and adds no per-call allocation (zero-byte allocation test). Torture waiver is in artifacts/verify/torture-waiver.txt.

Full verification and done-check results are recorded in /home/adam/projects/scratch/axial-coord/pi/cleanup-helper.report.md.
