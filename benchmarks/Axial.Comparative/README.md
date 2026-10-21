# Axial comparative benchmarks

This project runs the twelve [FIO](https://github.com/fio-fsharp/fio) Savina-style workloads against FIO 0.5.0-beta and Axial in the same BenchmarkDotNet process and job. FIO benchmark sources were ported from commit `fe10c67`; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the MIT licence.

The project is intentionally outside `Axial.slnx`, keeping FIO out of Axial's main build.

## Workloads and defaults

| Workload | Parameters |
|---|---|
| Bang | actors 50, 200; rounds 1,000, 5,000 |
| Big | actors 10, 25; rounds 100, 500 |
| BoundedBuffer | 4 producers, 4 consumers, capacity 10, 100,000 items/producer |
| Chameneos | 100 creatures, 100,000 meetings |
| Counting | 1,000,000 messages |
| Fibonacci | n 25, 30; threshold 12 |
| Fork | actors 1,000, 10,000, 50,000 |
| Philosophers | 20 philosophers, 10,000 rounds |
| Pingpong | rounds 10,000, 50,000, 150,000 |
| Threadring | actors 50, 100; rounds 1,000, 10,000 |
| Trapezoidal | workers 8, 16; 1,000,000 points |
| ZipRace | rounds 1,000, 10,000 |

Each Axial module preserves the corresponding FIO message counts and topology. FIO `Channel()` is an unbounded FIFO, mapped to `Queue.unbounded`. Fork builds its inputs per run, forks all no-op flows, then joins every fiber. Fibonacci defers every sequential leaf, forks the left branch, evaluates the right branch on the current fiber, and joins the left. Trapezoidal defers each slice's CPU work inside its parallel flow. BoundedBuffer and Philosophers retain FIO's actor-owned mutable state and arbitrator design rather than substituting another primitive.

The construction audit also checked Bang, Big, BoundedBuffer, Chameneos, Counting, Philosophers, Pingpong, and Threadring: their queues, arrays, mutable actor state, peer lists, and item distribution are created inside the outer `flow { }`, so each benchmark invocation rebuilds them. ZipRace creates each pair in its per-run loop. Its Axial branches can settle synchronously without scheduler dispatch, while FIO's `ZipPar` and `RaceFirst` fork fibers; that row compares each library's idiomatic combinator cost, not equivalent raw fiber cost. Axial uses default runtime settings and does not add FIO's internal message boxing.

## Run

```bash
dotnet run --project benchmarks/Axial.Comparative -c Release -- --validate
dotnet run --project benchmarks/Axial.Comparative -c Release -- --filter '*Pingpong*'
FIO_BENCH_RUNTIMES=Direct,WorkStealing-12-200-1,Axial dotnet run --project benchmarks/Axial.Comparative -c Release -- --filter '*Fork*' --job short
```

`FIO_BENCH_RUNTIMES` selects runtime rows. Every parameter retains its upstream `FIO_BENCH_*` override; `FIO_BENCH_WARMUP` and `FIO_BENCH_ITERATIONS` configure the default job. `--validate` runs both implementations of every workload with tiny inputs and a 30-second timeout.

## Reports

BenchmarkDotNet writes CSV files under `BenchmarkDotNet.Artifacts/results`.

```bash
python benchmarks/Axial.Comparative/scripts/plot.py --results-dir BenchmarkDotNet.Artifacts/results
python benchmarks/Axial.Comparative/scripts/compare.py --self-test
python benchmarks/Axial.Comparative/scripts/compare.py results-before results-after
```

`plot.py` requires pandas and Plotly; `compare.py` uses only the Python standard library.
