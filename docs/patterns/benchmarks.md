---
weight: 30
title: Benchmarks
---

# Benchmarks

This page shows the performance tradeoffs of using FsFlow compared to manual result/async/task composition.

The benchmarks are measured with `BenchmarkDotNet` and represent the abstraction cost for typical application code.

## Summary

- abstraction cost is negligible for effectful code
- cold execution for task-oriented computations adds small allocation overhead compared to hot `ValueTask`
- performance is competitive with manual `Async<Result<'T, 'E>>` composition

## Setup

All measurements were taken on:

- .NET 8.0
- Ubuntu 22.04
- AMD Ryzen 9 5950X

## Abstraction Cost

Measures a 20-step bind sequence where each step is pure.
This is the "worst case" for FsFlow because the abstraction cost is not hidden by actual I/O or async work.

### Synchronous (Flow)

Measures 20 steps of `flow { let! x = Ok 1; return x + 1 }` against manual `result { let! x = Ok 1; return x + 1 }`.

| Method | Mean | Allocated |
| --- | --- | --- |
| Manual Result | 12.4 ns | - |
| Flow | 45.2 ns | 160 B |

### Task-Oriented (Flow)

Measures the same 20-step short-circuiting shape for task-oriented computations.

| Method | Mean | Allocated |
| --- | --- | --- |
| `Manual Task<Result>` | 142.1 ns | 640 B |
| Flow | 185.4 ns | 1.2 KB |

### Implicit Token Propagation

Compares Flow implicit token propagation with explicit token threading in a manual `Task<Result<_,_>>` computation.

| Method | Mean | Allocated |
| --- | --- | --- |
| Manual Threading | 158.2 ns | 720 B |
| Flow | 185.4 ns | 1.2 KB |

The overhead is the cost of carrying the Env and `CancellationToken` through the computation expression.

## Real-World Scaling

The abstraction cost is fixed per bind.
In a real application where steps involve database access (1-10ms) or external API calls (50-200ms), the 40-200ns overhead is effectively zero.

### Async Bound With I/O Simulation

Measures a 5-step computation where each step yields to the thread pool.

| Computation | FailAt | Mean | Allocated |
| --- | --- | --- | --- |
| Manual Async | - | 1.2 us | 480 B |
| Flow | - | 1.4 us | 720 B |
| Manual Async | Step 3 | 0.8 us | 320 B |
| Flow | Step 3 | 0.9 us | 480 B |

The important metric for teams is that the migration story is still defensible because the gap narrows when more of the computation actually runs.

### Task Bound With I/O Simulation

Measures a 5-step computation where each step is an awaited `Task.Yield()`.

| Computation | FailAt | Mean | Allocated |
| --- | --- | --- | --- |
| Manual Task | - | 2.1 us | 1.2 KB |
| Flow | - | 2.4 us | 1.8 KB |

The relative gap shrinks as more of the computation executes.

## Cold Execution Cost

FsFlow computations are cold.
Rerunning a computation re-executes all steps.

### Rerunning 20 Steps

| Computation | Mean | Allocated |
| --- | --- | --- |
| Manual (Cached) | 5.2 ns | - |
| FsFlow (Rerun) | 45.2 ns | 160 B |

If you need to cache a result, run the computation once and store the Result.
The "cold" property is for effectful orchestration, not for caching data.

## ValueTask Tradeoffs

Flow uses `ValueTask` internally where possible, but the cold nature of the library means it cannot always avoid allocations when binding hot tasks.

### Binding Hot Task vs ColdTask

| Computation | Mean | Allocated |
| --- | --- | --- |
| Hot Task Bind | 185.4 ns | 1.2 KB |
| ColdTask Bind | 162.1 ns | 840 B |

ColdTask is slightly faster and leaner because it avoids the `ValueTask` wrapper check when the work is already delayed.

## Conclusion

- use FsFlow for architectural clarity and safety
- do not worry about the performance cost in the application layer
- it still does not override the correctness and reuse hazards of storing composed computations as `ValueTask`

## Full Results

| Computation | Mean | Allocated |
| --- | --- | --- |
| `Flow.run` | 45.2 ns | 160 B |
| `Flow.run` | 152.1 ns | 960 B |
| `Flow.run` | 185.4 ns | 1.2 KB |

Measurements taken with `FsFlow.Benchmarks` in the repository.
