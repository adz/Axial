# Cleanup helper

ProcessRun.bestEffort centralizes ten cleanup catches; the result-selecting isLive catch remains separate. Inline action invocation avoids allocating capturing closures at call sites. OutOfMemoryException, StackOverflowException and AccessViolationException propagate (synthetic OOM tested); ordinary exceptions remain tolerated with unchanged disposal grouping/order.

ProcessServiceTests characterization cases passed on old code (82 tests). Helper tests were committed first and failed compilation without the helper; final class has 84 tests and passed 20 consecutive runs. User explicitly authorized passing-before-and-after characterization tests as a fail-first waiver. Artifacts record the waiver and separate helper red proof. No public API change; no benchmark required because the helper is inline and adds no per-call allocation. Torture waiver is in artifacts/verify/torture-waiver.txt.

Full verification and done-check results are recorded in /home/adam/projects/scratch/axial-coord/pi/cleanup-helper.report.md.
