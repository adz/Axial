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

## Final run: after the cancellation primitives (main `a88ca6dfd`, 2026-10-28)

Same quiet-machine setup and suites. The change is behaviour-preserving: `CancellationToken.None` is routed through
`Cancellation.never` and `Cancellation.uninterruptible`.

- Allocation is unchanged against the after-race run in every FiberCost case (ForkJoin 2,812, ForkResource 4,592,
  ZipPar2 1,280, SequencePar8 3,704, SequencePar64 22,296, Race 1,784, TraversePar64 39,360,
  RunOnThreadPool 1,182 B/op). Against the morning baseline, the only movement is Race −27%; the rest are within ±0.2%.
- Comparative Axial cases: time moved −13% to −35% on Chameneos/Counting/Trapezoidal. That is cross-session noise in
  our favour; the code path is unchanged. Chameneos 200×1000 allocation +6% is contended-scheduling variance, as noted above.
- `Flow bind x100`: 31.11 KB, after 32.69 and 34.96/34.43 KB on earlier runs. That confirms the earlier bind
  "regression" was noise.

Net for the day: Race −27% allocation and −26% time. No case regressed in allocation beyond run-to-run variance.

## Final run: after the guardrails and adapter work (main `ed2c2f891`, 2026-10-28)

The same quiet-machine setup and both suites, 41 minutes, after the guardrails policy, the adapter conversions and
the `CancellationToken.None` removal. The core runtime changed only in the `FiberLocal` id allocator and comments. The
Axial runtime paths measured here are unchanged.

- **Allocation.** No Axial case moved by more than 2% against the morning baseline, the cancellation-primitives run or
  the race run. The allocation flags in the comparison tool are FIO's own runtimes (Bang 200×5000 Direct 27 GB,
  Fibonacci WorkStealing +5.6%).
- **Core suite (100 cases).** Geometric-mean time is 0.95× the morning baseline and 1.015× the cancellation-primitives
  run. The slowest cases against that run are the `DotNetControl` rows (plain .NET code Axial does not touch), up
  18–21%, which sizes the run-to-run noise.
- **Against FIO** the shape is the same as the morning baseline:

  | Workload | Axial ÷ best FIO, time | Axial ÷ best FIO, allocation |
  |---|---|---|
  | Bang | 0.58–0.73 | 0.49–0.51 |
  | Fork | 0.75–0.93 | 0.98–0.99 |
  | Counting | 0.77 | 0.72 |
  | ZipRace | 0.07–0.08 | 0.22 |
  | Pingpong, Threadring | 1.36–1.70 | 1.47–1.49 |

  Pingpong and Threadring remain the one place Axial is clearly behind FIO (queue hand-off, follow-up 1 above).
- **Two Axial cases were flagged slower in the full run:** Big 25×100 (51.8 ms against 44.5) and Pingpong 10,000
  (49.8 ms against 44.5), with FIO's own rows swinging up to 80% in the same run. Two reruns of those workloads:

  | Case | Baseline | Full run | Rerun 1 | Rerun 2 |
  |---|---|---|---|---|
  | Big 25×100, Axial | 44.5 ms | 51.8 | 46.2 | 46.6 |
  | Pingpong 10,000, Axial | 44.5 ms | 49.8 | 39.0 | 43.8 |

  Both are back within a few percent of the baseline, so the flags were noise. Raw data is in
  `~/projects/scratch/axial-coord/bench/final-2026-10-28*` (not in the repo).

No performance regression from the guardrails and adapter work.
