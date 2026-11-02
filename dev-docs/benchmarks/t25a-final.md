# T25a final benchmark harness

FiberCost cases use unit and the runtime live defaults. They neither sleep nor log. Do not construct
withClock/withLogSink wrappers inside the measured run helper: that measures two extra Flow values and runtime
scope installs, not the root/fiber operation. Explicit clock/log installation is measured by separate platform tests.
The former per-invocation-wrapper harness was recorded before this change in
`~/projects/scratch/axial-coord/bench/t25a-final/fibercost-before.log`.

Build Release with `-p:AxialGuardrailsEnabled=false`, then run the built DLL directly from
`artifacts/bin/Axial.Benchmarks/release`, one process at a time:

```bash
dotnet Axial.Benchmarks.dll --filter '*PlatformReadBenchmarks*' --job short --inProcess --memory
dotnet Axial.Benchmarks.dll --filter '*FiberCostBenchmarks*' --job short --inProcess --memory
```

Final measurements and baseline comparisons are recorded in the phase report
`~/projects/scratch/axial-coord/pi/t25a-E.report.md`, with raw logs under `bench/t25a-final/` beside the coordinator.
Use the allocation columns for the 2% gate; timing is diagnostic and must include Error/StdDev rather than treating
short-job noise as a performance change.
