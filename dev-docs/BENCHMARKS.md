# Runtime benchmarks

`benchmarks/Axial.Benchmarks` measures workflow execution overhead. Its operations deliberately do little application
work; results do not predict HTTP throughput, database latency, or production tail latency.

## Cases and boundaries

- `SynchronousCompletionBenchmarks` measures a primitive and a short pipeline through `RunSynchronously` and
  `StartAsValueTask`. The candidate ValueTask implementation omits scopes and runtime context; it is a smaller
  contract, not an alternative production executor.
- `CompositionChainBenchmarks` executes prebuilt, left-nested chains of 100 steps. The Task and Async variants use
  the corresponding Flow interop adapters. They complete without scheduler yields.
- `CompositionDepthBenchmarks` measures left-nested chains at depths 20, 80, 100 and 200, spanning the runtime's
  stack-guard boundary. Its fixed scheduling cost means the depth-100 result is not a constant per-bind cost.
- `TaskRailwayBenchmarks` and `AsyncRailwayBenchmarks` measure failure at the first or twentieth step. A failure
  at the first step largely measures startup and propagation through the remaining prebuilt chain.
- `InteropCompletionBenchmarks` constructs and executes recursive chains of 20 steps. `Suspend=false` uses completed
  operations; `Suspend=true` uses `Task.Yield` in the source operation. Async cases use `Async.AwaitTask` over the
  same operation. Yields exercise scheduler transitions, not network or timer latency. Flow Async interop includes
  the Async bridge on every step; compare it with the direct Async case as well as the Task cases. The ValueTask
  cases wrap the same Task source; they do not isolate inline-result or pooled IValueTaskSource performance.
- `RecursiveLoopBenchmarks` compares direct tail recursion with CE `while`, both with synchronous bodies and with
  yielding Task bodies. Workflow construction occurs during execution, as it does in recursive application code.
- `ParallelCompositionBenchmarks` measures concurrent combinator overhead with trivial successful branches. It
  does not measure parallel application throughput or cancellation under load.
- Environment and cancellation cases measure projections/checkpoints, not service I/O or cancellation latency.

All Flow cases execute through public boundaries, including root scope creation and cleanup. Comparing them with
Task/Async therefore includes the cost of Axial's additional semantics. Most existing jobs use short runs; small
mean differences need longer repeats and stable controls before they justify a runtime change.

## Measurement workflow

Build in Release, then run benchmarks without other builds, tests, or documentation execution running concurrently:

```bash
dotnet build benchmarks/Axial.Benchmarks -c Release
dotnet run -c Release --no-build --no-restore --project benchmarks/Axial.Benchmarks -- --filter '*'
```

Use `--filter '*RecursiveLoopBenchmarks*'` for loop work and `--filter '*SynchronousCompletionBenchmarks*'` for root
execution work. Use identical benchmark definitions against the before and after runtimes. Separate changes when
probing causes, then rerun the entire suite on the final implementation. Keep reports and measured binaries under
`artifacts/benchmarks/`; they are generated outputs and remain untracked.

Compare allocation counts alongside time. Allocated bytes measure churn, not memory retained by a long-running
workflow; `MemoryRetentionTests` and the recursive reproduction cover retention separately. Raw Task/Async/Ply
controls help identify machine-wide timing changes. InProcess cases share a host: preceding cases can change
thread-pool and JIT state. Use the same complete case inventory and order in both runs; separately running new
cases does not make an older full-suite baseline comparable. Cross-session timing differences alone do not isolate
a change.

Fable, NativeAOT and solution restores share intermediate paths within a worktree. Run them serially, and restore
again before a solution build after a Fable restore. Runtime optimizations must preserve source single-consumption,
stack safety, runtime-context restoration, and waiting for owned cleanup in every completion mode.
