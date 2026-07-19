---
weight: 80
title: Benchmarks
---

# Benchmarks

This page shows the performance tradeoffs of using Axial compared to manual composition across the runtime shapes it supports.

The benchmark harness lives in [benchmarks/Axial.Benchmarks/Suites.fs](https://github.com/adz/Axial/blob/main/benchmarks/Axial.Benchmarks/Suites.fs) and the shared helpers live in [benchmarks/Axial.Benchmarks/Common.fs](https://github.com/adz/Axial/blob/main/benchmarks/Axial.Benchmarks/Common.fs).
The Fable runner lives in [benchmarks/Axial.Benchmarks.Fable/Program.fs](https://github.com/adz/Axial/blob/main/benchmarks/Axial.Benchmarks.Fable/Program.fs) and shares its workload definitions from [benchmarks/Axial.Benchmarks.Fable/Shared.fs](https://github.com/adz/Axial/blob/main/benchmarks/Axial.Benchmarks.Fable/Shared.fs).
The Fable benchmark project is self-contained by source inclusion, so it can be compiled directly with Fable against the library code in [src/Axial](https://github.com/adz/Axial/tree/main/src/Axial). Its local tool manifest lives in [benchmarks/Axial.Benchmarks.Fable/mise.toml](https://github.com/adz/Axial/blob/main/benchmarks/Axial.Benchmarks.Fable/mise.toml), with [benchmarks/Axial.Benchmarks.Fable/package.json](https://github.com/adz/Axial/blob/main/benchmarks/Axial.Benchmarks.Fable/package.json) for Node ESM execution and [scripts/run-fable-benchmarks.sh](https://github.com/adz/Axial/blob/main/scripts/run-fable-benchmarks.sh) for the target-specific runner.

The implementation split matters:

- [src/Axial/Core.fs](https://github.com/adz/Axial/blob/main/src/Axial/Core.fs) defines `Execution<'value, 'error>` and flips its concrete shape by compiler target.
- [src/Axial/Flow.fs](https://github.com/adz/Axial/blob/main/src/Axial/Flow.fs) exposes execution members such as `ToTask`, `ToValueTask`, `ToAsync`, and `RunSynchronously`.
- [benchmarks/Axial.Benchmarks/Suites.fs](https://github.com/adz/Axial/blob/main/benchmarks/Axial.Benchmarks/Suites.fs) shows the manual baselines beside the `Flow` versions.
- [scripts/run-benchmarks.sh](https://github.com/adz/Axial/blob/main/scripts/run-benchmarks.sh) prompts before starting the .NET benchmark run so you can stop other processes first.

## Summary

- `Flow` stays competitive with manual `Result`, `Async<Result<_,_>>`, and `Task<Result<_,_>>` composition
- the .NET effect shape is `ValueTask<Exit<'value, 'error>>`
- the Fable effect shape is `Async<Exit<'value, 'error>>`
- the same benchmark source can be reused across .NET, Node, Erlang, and any other supported Fable backend

## Setup

Recorded .NET measurements in this repository were taken with:

- .NET SDK 10.0.201
- .NET runtime 10.0.5
- F# 10.0
- BenchmarkDotNet 0.15.8

The current repo toolchain also builds with .NET SDK 10.0.203, .NET runtime 10.0.7, and F# 10.0. The benchmark source does not change between those toolchains.

## Runtime Coverage

Each runtime should show the same comparison:

| Runtime | Without Flow | With Flow | Notes |
| --- | --- | --- | --- |
| .NET | `Result`, `Async<Result<_,_>>`, `Task<Result<_,_>>`, and raw `Task` baselines | `ToTask` / `ToValueTask` returning `Exit<'value, 'error>` asynchronously | Measured with BenchmarkDotNet |
| Fable on Node | manual `Result` and reader composition | `ToAsync` returning `Async<Exit<'value, 'error>>` | Run through `scripts/run-fable-benchmarks.sh` |
| Fable on Erlang | manual `Result` and reader composition | `ToAsync` returning `Async<Exit<'value, 'error>>` | Run through `scripts/run-fable-benchmarks.sh` |
| Other supported Fable backend | manual `Result` and reader composition | `ToAsync` returning `Async<Exit<'value, 'error>>` | Use the same benchmark source whenever a backend is available |

The Fable runner uses the same benchmark source on each backend, with the sync-result and reader-propagation comparisons shown below for Node and Erlang. The other-backend row is the same runner shape, ready for any additional Fable target that becomes available.

## .NET Results

The measured .NET run in this repository used:

- .NET SDK 10.0.203
- .NET runtime 10.0.7
- F# 10.0
- BenchmarkDotNet 0.15.8

The tables below are taken from the joined BenchmarkDotNet report for the current tree.

### Reader Overhead

| Method | Mean | Allocated |
| --- | --- | --- |
| `Manual env passing x10` | 74.02 ns | 80 B |
| `Flow task adapter localEnv x10` | 256.06 ns | 560 B |
| `AsyncLocal updates x10` | 819.32 ns | 2,000 B |

### Task Railway

| Method | FailAt | Mean | Allocated |
| --- | --- | --- | --- |
| `Direct Task<Result>` | 1 | 179.20 ns | 256 B |
| `Flow task adapter` | 1 | 2.718 us | 5,944 B |
| `FsToolkit taskResult` | 1 | 167.47 ns | 336 B |
| `Direct Task<Result>` | 20 | 1.749 us | 3,392 B |
| `Flow task adapter` | 20 | 3.865 us | 7,320 B |
| `FsToolkit taskResult` | 20 | 2.742 us | 6,512 B |

### Async Railway

| Method | FailAt | Mean | Allocated |
| --- | --- | --- | --- |
| `Direct Async<Result>` | 1 | 7.048 us | 1,000 B |
| `Flow async adapter` | 1 | 8.907 us | 7,977 B |
| `Direct Async<Result>` | 20 | 8.653 us | 7,393 B |
| `Flow async adapter` | 20 | 11.378 us | 9,353 B |
| `FsToolkit asyncResult` | 20 | 9.226 us | 12,561 B |

### Cancellation

| Method | Mean | Allocated |
| --- | --- | --- |
| `Manual token Task` | 229.87 ns | 624 B |
| `CancellableTask` | 290.42 ns | 504 B |
| `Explicit token Task<Result>` | 367.77 ns | 672 B |
| `Flow task adapter` | 1.079 us | 3,136 B |

### Composition Chain

| Method | Mean | Allocated |
| --- | --- | --- |
| `Flow map x100` | 8.342 us | 12.024 KB |
| `Flow bind x100` | 11.090 us | 18.424 KB |
| `Flow task adapter bind x100` | 11.450 us | 28.096 KB |
| `Direct Task<Result> bind x100` | 11.762 us | 21.504 KB |
| `Direct Async<Result> bind x100` | 18.083 us | 34.443 KB |
| `Flow async adapter bind x100` | 26.710 us | 44.739 KB |
| `Raw Task bind x100` | 7.804 us | 17.488 KB |

### Synchronous Completion

| Method | Mean | Allocated |
| --- | --- | --- |
| `Candidate ValueTaskFlow` | 68.36 ns | 96 B |
| `Flow task adapter` | 258.24 ns | 752 B |
| `Ply vtask` | 209.37 ns | 128 B |

The practical read is unchanged: `Flow` stays competitive with the direct baselines, and the extra cost is a fixed orchestration cost rather than a function of the actual business logic.

### Schema JSON Codec

The codec suites measure `Axial.Schema.Codec` — the JSON codec compiled from a `Schema<'model>` declaration — on a realistic aggregate (seven primitive fields, one nested record, and two collections) against `System.Text.Json` on the same model. Both suites live in [benchmarks/Axial.Benchmarks/CodecSuites.fs](https://github.com/adz/Axial/blob/main/benchmarks/Axial.Benchmarks/CodecSuites.fs).

Run them:

```bash
dotnet run -c Release --project benchmarks/Axial.Benchmarks -- --filter "*JsonCodecBenchmarks*" "*BoundaryParseBenchmarks*"
```

Measured with a BenchmarkDotNet short job on the recorded toolchain:

| Method | Mean | Allocated |
| --- | --- | --- |
| `System.Text.Json Serialize` | 1.44 us | 1.11 KB |
| `Axial Json.serialize` | 1.55 us | 1.44 KB |
| `Axial Json.deserializeBytes` | 2.85 us | 2.46 KB |
| `Axial Json.deserialize` | 3.10 us | 2.84 KB |
| `System.Text.Json Deserialize` | 3.11 us | 2.01 KB |

The codec compiles once per schema and runs with no reflection, so it stays on par with `System.Text.Json`'s reflection-based serializer while remaining AOT- and trimming-safe by construction. `deserializeBytes` skips the string-to-UTF-8 conversion and is the faster decode entry point when the payload already arrives as bytes.

The boundary suite compares the trusted codec lane against full boundary parsing — `JsonDocument` to `Data` to `Schema.parse` with complete path-aware diagnostics:

| Method | Mean | Allocated |
| --- | --- | --- |
| `Axial Json.deserialize (trusted path)` | 3.15 us | 2.84 KB |
| `JsonDocument + Data + Schema.parse (boundary lane)` | 19.78 us | 27.71 KB |

That gap is the price of diagnostics, redisplayable structured data, and constraint checking, and it is why the two paths exist: parse untrusted input where the diagnostics pay for themselves, and use the compiled codec for trusted payloads such as internal services, storage, and queues.

## Fable Results

The Fable runner is built from the source-included benchmark project in [benchmarks/Axial.Benchmarks.Fable/Axial.Benchmarks.Fable.fsproj](https://github.com/adz/Axial/blob/main/benchmarks/Axial.Benchmarks.Fable/Axial.Benchmarks.Fable.fsproj) and uses the toolchain pins in [benchmarks/mise.toml](https://github.com/adz/Axial/blob/main/benchmarks/mise.toml) plus [benchmarks/Axial.Benchmarks.Fable/mise.toml](https://github.com/adz/Axial/blob/main/benchmarks/Axial.Benchmarks.Fable/mise.toml).

### Fable on Node

Measured with Fable 5.0.0 and Node 26.1.0.

#### Sync Result

| Method | Mean |
| --- | --- |
| `manual result` | 1,600.00 ns |
| `flow` | 6,400.00 ns |

#### Async Result

| Method | Mean |
| --- | --- |
| `manual async result` | 100.00 ns |
| `flow` | 17,300.00 ns |

#### Reader Propagation

| Method | Mean |
| --- | --- |
| `manual env passing` | 100.00 ns |
| `flow` | 3,000.00 ns |

### Fable on Erlang

Measured with Fable 5.0.0, Erlang 27.2.2, and rebar 3.24.0.

#### Sync Result

| Method | Mean |
| --- | --- |
| `manual result` | 1,276.00 ns |
| `flow` | 8,766.20 ns |

#### Async Result

| Method | Mean |
| --- | --- |
| `manual async result` | 78.90 ns |
| `flow` | 100,609.00 ns |

#### Reader Propagation

| Method | Mean |
| --- | --- |
| `manual env passing` | 1,116.60 ns |
| `flow` | 9,330.70 ns |

## Conclusion

- use Axial for architectural clarity and safety
- expect some orchestration overhead even for local reader and synchronous composition
- treat Fable BEAM async as the highest-cost case in these microbenchmarks
- keep the runtime comparison tied to the actual target backend, because the platform execution handle shape changes between .NET and Fable
- use the benchmark source links above when you want to inspect how the manual and `Flow` implementations differ

## Benchmark Map

The actual benchmark suites and the method pairs they compare are:

- `ReaderOverheadBenchmarks`: `ManualEnvPassingX10` vs `TaskFlowLocalEnvX10`
- `AsyncRailwayBenchmarks`: `DirectAsyncResult` vs `Flow async adapter`
- `TaskRailwayBenchmarks`: `DirectTaskResult` vs `Flow task adapter`
- `CompositionChainBenchmarks`: `FlowMapX100`, `FlowBindX100`, `AsyncFlowBindX100`, `TaskFlowBindX100`, `DirectAsyncResultBindX100`, `DirectTaskResultBindX100`, `RawTaskBindX100`
- `CancellationFlowBenchmarks`: `ExplicitTokenTaskResult` vs `Flow task adapter`
- `CancellableTaskBenchmarks`: `ManualTokenTask` vs `CancellableTask`
- `SynchronousCompletionBenchmarks`: `CandidateValueTaskFlow` vs `Flow task adapter`
- `JsonCodecBenchmarks`: `Axial Json.serialize`/`Json.deserialize` vs `System.Text.Json`
- `BoundaryParseBenchmarks`: trusted codec lane vs `Data` + `Schema.parse` boundary lane

The .NET benchmark report is generated from `Axial.Benchmarks`; the Fable runner is separate and uses the same comparison vocabulary without pretending the runtime shape is the same.
