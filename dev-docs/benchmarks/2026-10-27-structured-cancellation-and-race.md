# Benchmarks, 2026-10-27: after structured cancellation, before and after the race-allocation fix

Quiet machine (all agents and builds stopped), 8 cores, 15 GB, .NET SDK 10.0.402. Raw BenchmarkDotNet output and
summaries are kept outside the repo in `~/projects/scratch/axial-coord/bench/{baseline,after-race}/`.

- **Baseline:** main `acca6316e` (structured cancellation merged). Core suite: 100 cases, 14 min. Comparative FIO/Savina
  suite: 84 cases, 24 min (runtimes: FIO Direct, FIO WorkStealing-12-200-1, Axial). `--validate` passed all 12 workloads.
- **After:** main `54649e650` (race allocation fix `c0ce90582`; the other commits are guardrails tooling and docs, with no runtime
  change). Same suites and settings.

## Axial against FIO (baseline)

Ratio = Axial ÷ the faster FIO runtime; below 1 means Axial is faster.

| Area | Workloads | Time | Allocation |
|---|---|---|---|
| Fan-out, fork/join, compute | Fork, Fibonacci, Bang, Big (small sizes), Counting | 0.65–0.98 | 0.49–0.99 |
| Combinators | ZipRace | 0.07–0.10 | 0.27 |
| 1:1 message hand-off | Pingpong, Threadring | **1.35–1.73** | **1.46–1.48** |
| Contended queues | BoundedBuffer, Chameneos, Philosophers | 1.13–1.36 | 0.67–1.10 |

Axial's fiber and fork cost is competitive with FIO. The queue hand-off path (offer → wake taker → resume) is the
clear weakness, at ~1.5× FIO in time and bytes per message. It is the next performance study.

## Effect of the race fix (baseline → after)

| Case | Time | Allocation |
|---|---|---|
| FiberCost Race | 986.8 → 725.7 ns (−26%) | 2,432 → 1,784 B (−27%) |
| race x1000 | 1,106.6 → 770.6 µs (−30%) | 2,313.7 → 1,680.9 KB (−27%) |
| Comparative ZipRace, Axial, 1,000 rounds | 1.577 → 1.247 ms (−21%) | 3.20 → 2.58 MB (−19%) |
| Comparative ZipRace, Axial, 10,000 rounds | 16.96 → 12.71 ms (−25%) | 31.97 → 25.79 MB (−19%) |

Race is back below its pre-structured-cancellation allocation (1,728 B) and still waits for the loser's cleanup.

## Noise, not regressions

- `Flow bind x100` allocation: 32.69 KB (baseline), then 34.96 KB and 34.43 KB on two after-runs. `Flow map x100`
  moved the other way: 27.58 → 26.57 → 24.94 KB. Neither code path changed (the only runtime diff is `Flow.race`). These
  short-run asynchronous cases are not allocation-deterministic to a few percent.
- Time changes of ±10–30% on other cases with no code change: left-nested bind, fork/join trivial, Trapezoidal
  (sub-millisecond), and FIO's own runtimes (e.g. FIO WorkStealing Big 25×500 −41%). These are cross-session noise.
  Compare only within one session, or repeat.
- Comparative Chameneos 50×1000, Axial allocation +8% (46.9 → 50.7 MB), time −6%. This is contended scheduling,
  where allocation depends on interleaving.

## Follow-ups

1. Queue hand-off study (Pingpong/Threadring ~1.5× FIO).
2. To claim a time change, rerun the case in the same session as its baseline.
