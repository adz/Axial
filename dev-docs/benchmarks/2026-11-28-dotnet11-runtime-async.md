# .NET 11 runtime and runtime async (2026-11-28)

Question: does .NET 11, and its runtime-async feature, make Axial faster?

## Setup

Three builds of `benchmarks/Axial.Benchmarks` at 3e55f053d, BenchmarkDotNet `--job short --inProcess --memory`,
run one after another on an otherwise idle machine:

| Variant | Runtime | Axial build | `Platform.fold` slow path |
| --- | --- | --- | --- |
| net10 | .NET 10.0 | net8.0 | `task { }` |
| base | .NET 11.0 | net11.0 | `task { }` |
| ra | .NET 11.0 | net11.0, `LangVersion=preview`, FSharp.Core 11.0.101 | runtime async (`__runtimeAsyncReturnValueTask` + `AsyncHelpers.Await`) |

The prototype (not merged) added a `net11.0` target to Axial, Console, FileSystem and Process, and replaced only the
suspended path of `Platform.fold`, the core bind, behind `-p:AxialRuntimeAsync=true`. The compiler emitted the method
with `MethodImplAttributes.Async`, so it was real runtime async.

## Results (mean)

| Case | net10 | base | ra |
| --- | ---: | ---: | ---: |
| ForkJoin | 3,966 ns | 3,704 ns | 4,211 ns |
| ForkResource | 6,436 ns | 6,687 ns | 6,476 ns |
| ZipPar2 | 657 ns | 639 ns | 614 ns |
| Race | 886 ns | 822 ns | 828 ns |
| SequencePar64 | 18,880 ns | 18,686 ns | 17,582 ns |
| TraversePar64 | 22,697 ns | 22,341 ns | 21,381 ns |
| RunOnThreadPool | 1,801 ns | 1,589 ns | 1,617 ns |
| Flow Task interop x20, completed | 3.61 µs | 3.44 µs | 3.55 µs |
| Flow Task interop x20, `Task.Yield` each step | 36.5 µs | 32.1 µs | 32.8 µs |

Allocation was the same in all three to within 10 B.

A separate probe compared one F# `task { }` step with the same step written with the runtime-async intrinsics, called
from ordinary code:

| Case | `task { }` | runtime async |
| --- | ---: | ---: |
| Awaited value already complete | 13.9 ns, 0 B | 21.0 ns, 24 B |
| Awaited value suspends (`Task.Yield`) | 747 ns, 223 B | 953 ns, 383 B |

## Findings

- **The .NET 11 runtime alone helps**: about 5–12% on fork, race, thread-pool and suspending interop cases, with no
  code change. Applications get this by running on .NET 11; Axial's net8.0 build is enough.
- **Runtime async in `fold` gives nothing measurable.** The differences between base and ra are within run-to-run
  spread, in both directions.
- Why: published runtime-async gains come from async methods calling async methods, where no `Task` is created
  between them, and from methods that complete without suspending. Axial already skips the state machine when an
  execution has completed (`IsCompletedSuccessfully` fast path). And every Flow step is an F# closure called through
  `FSharpFunc.Invoke`, which returns a `ValueTask` to ordinary code, so each runtime-async method has to produce a
  real task at its boundary. The probe shows that boundary costs more than F#'s resumable `task { }`.
- Adopting it would also need `LangVersion=preview` for a shipped library and an FSharp.Core 11 floor on a new
  `net11.0` target.

## Decision

Do not adopt runtime async now. Revisit if F# can mark closures or a non-closure Flow representation as
runtime-async end to end (the Flow-as-data, many-runtimes idea), or when the feature leaves
preview in F#. Consider moving the benchmark and test projects to `net11.0` so measurements reflect the current
runtime.
