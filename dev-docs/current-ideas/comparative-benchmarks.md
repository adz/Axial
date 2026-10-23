# Comparative benchmarks: Axial and FIO, side by side

Purpose: these benchmarks exist to understand Axial's concurrency behaviour (fibers, queues, contention, fork/join,
parallel combinators) against an independent reference. They are a diagnostic tool first; publishing numbers is a
later, optional step.

Status: steps 1–4 of the order of work are implemented in `benchmarks/Axial.Comparative` (all twelve workloads, with
`--validate` checking both libraries compute the same results). Not done: a full recorded run on an idle machine and
the performance page. What the work has taught so far is in `benchmarks/Axial.Comparative/FINDINGS.md`, for example
that `Flow.zipPar`/`sequencePar` start branches inline, so CPU-bound branches need `Flow.runOn`/`Flow.onThreadPool`
or `Flow.fork` to run in parallel.

## Why

Axial's benchmark suite (`benchmarks/Axial.Benchmarks`, see `BENCHMARKS.md`) measures workflow overhead against
Task, Async, Ply and IcedTasks. It says nothing about concurrency workloads: fibers, queues, contention, fork/join.
Those are the claims a reader weighing an effect system cares about, and we publish no numbers for them.

[FIO](https://github.com/fio-fsharp/fio) (`FSharp.FIO`, MIT, .NET 10) is the closest peer: an F# effect system
modelled on ZIO, from a DTU thesis, with a different concurrency model. It ships its own fiber runtimes (Direct on the
thread pool, Polling, Signaling, and a default WorkStealing scheduler) where Axial runs fibers on the .NET thread pool.
Its benchmark suite is public, BenchmarkDotNet-based, and drawn from the Savina actor suite. Porting that suite gives
Axial concurrency numbers that a reader can check against a known, independent reference.

## Principles

- **Same machine, same session, same harness.** Published numbers from another machine are not comparable. Every
  comparison runs both libraries in one BenchmarkDotNet invocation on one machine, with the machine, OS, .NET SDK,
  library versions and commit recorded next to the results.
- **Port the workload, not the numbers.** Each Axial benchmark does the same work as its FIO counterpart: same
  parameters, same message counts, same topology, same termination condition. The workload parameters are the
  contract; where Axial's primitives differ, the doc for that workload says how and why.
- **Their code, credited.** FIO is MIT. Ported benchmark code keeps FIO's copyright notice and licence in the ported
  files and a NOTICE entry; each file names the FIO source file and commit it was translated from.
- **Honest defaults on both sides.** FIO runs with its own default runtime configuration
  (`WorkStealing-12-200-1`) and with `Direct`, which is the closest model to Axial's thread-pool fibers. Axial runs
  with its defaults. No tuning one side for the benchmark.
- **Time and allocations together.** Every benchmark carries `[<MemoryDiagnoser>]`. Allocations are deterministic
  and comparable across sessions; time is only comparable within a session.

## The workloads

FIO's twelve workloads, parameters (environment variable defaults), and the Axial primitives each maps to. Names are
Axial's current API: `Flow.fork` returns a `Fiber`; `Fiber.join`/`Fiber.await` wait for it; message passing uses
`Queue` (`Queue.unbounded`, `Queue.bounded`, `Queue.offer`, `Queue.take`); one-shot replies use `Deferred`.

| Workload | Measures | FIO defaults | Axial mapping |
|---|---|---|---|
| Pingpong | Message delivery between two actors | rounds 10k, 50k, 150k | Two forked fibers, one `Queue` each way |
| Threadring | Message passing and context switching in a ring | actors 50, 100; rounds 1k, 10k | N fibers, one `Queue` per link |
| Bang | Many-to-one messaging | actors 50, 200; rounds 1k, 5k | N senders, one shared `Queue` |
| Big | Mailbox contention, many-to-many | actors 10, 25; rounds 100, 500 | N fibers, one `Queue` each, random peers |
| Counting | Single-actor throughput, request/response | 1M messages | One fiber, `Queue` in, `Deferred` reply |
| Fork | Fiber creation and scheduling | 1k, 10k, 50k fibers | `Flow.fork` N trivial flows, `Fiber.join` all |
| Fibonacci | Recursive fork/join tree | n 25, 30; cutoff 12 | Fork left, evaluate right, join (fork/join) above the cutoff |
| Trapezoidal | CPU-bound master/worker map-reduce | workers 8, 16; 1M points | `Flow.forEachPar` or forked workers plus a result `Queue` |
| BoundedBuffer | Producer/consumer backpressure | 4 producers, 4 consumers, capacity 10, 100k items each | `Queue.bounded 10` |
| Philosophers | Resource arbitration, deadlock freedom | 20 philosophers, 10k rounds | `Semaphore` per fork, or STM `TRef`s (see below) |
| Chameneos | Rendezvous and shared-broker contention | 100 creatures, 100k meetings | Broker fiber with `Queue` and `Deferred` replies |
| ZipRace | Parallel-combinator overhead | rounds 1k, 10k | `Flow.zipPar` and `Flow.race` per round |

Per-workload decisions to settle while porting, and to record in the ported file:

- **Channel semantics.** FIO's channel boxes every message to `obj`. Axial's `Queue` is generic. Check whether FIO's
  actor channels are bounded or unbounded and match it (`Queue.unbounded` unless FIO bounds them). Note the boxing
  difference in the results page; do not add boxing to Axial to match.
- **Philosophers.** Two honest ports exist: `Semaphore` per fork (closest to a lock-based Savina port) and STM
  (`TRef` per fork, retry until both are free, the idiomatic Axial answer). Port whichever FIO's version resembles as
  the primary, and add the other as a clearly labelled variant.
- **Termination.** Each Savina workload ends on a counted condition; Axial ports use the same counter and join or
  await every fiber, so no work escapes the measured interval.

## Running side by side

One BenchmarkDotNet project, `benchmarks/Axial.Comparative`, outside `Axial.slnx` (like `Axial.Build.Tests`), so the
FIO package never enters the main build:

- References Axial by `ProjectReference` and FIO by `PackageReference` to a pinned `FSharp.FIO` version.
- F# has no `extern alias`, and none is needed: FIO and Axial use different namespaces, so the benchmark files open
  only what they use and qualify any clashing type names.
- Each workload is one benchmark class with an FIO method as the baseline and Axial methods beside it, so
  BenchmarkDotNet's Ratio column reads directly as "Axial time relative to FIO". The FIO runtime is a parameter, so
  one class reports Axial against both FIO `Direct` and FIO `WorkStealing`.
- Parameters are read from the same `FIO_BENCH_*` environment variables with the same defaults, so FIO's documented
  commands and sweeps apply unchanged.
- Results land in `BenchmarkDotNet.Artifacts/results/*-report.csv`, the input FIO's `plot.py` and `compare.py`
  read. Use them unchanged where they accept the extra Axial rows; otherwise copy them with credit and the smallest
  change needed.

BenchmarkDotNet already runs each benchmark case in its own process, so the two libraries do not share JIT or
thread-pool state within a case.

### Known noise

- **Idle worker threads.** FIO's custom runtimes keep worker threads alive between operations; FIO's README notes
  this adds wall-clock-based allocation. Use the same `MinIterationTime` dilution FIO uses when comparing absolute
  allocation numbers, and say so on the results page.
- **Isolation.** As `BENCHMARKS.md` already requires, run alone: no other `dotnet` or `node` job on the machine.
- **Run length.** All 12 workloads × parameter sets × 2 FIO runtimes + Axial is long. CI runs a `--job Short`
  smoke of Pingpong and Fork only, to catch hangs and gross regressions; full runs are manual and recorded.

## Publishing the numbers

A user-facing page, `docs/15-notes/<n>-performance.md`, replaces scattered claims:

- What each suite measures and does not measure (workflow overhead versus concurrency workloads versus HTTP, which
  neither covers; see the Kestrel adapter benchmark idea below).
- One table per suite from the latest recorded run: Mean, Ratio, Allocated, with the machine, OS, SDK, Axial
  version, FIO version and date in the caption, and a link to the raw CSVs committed under
  `benchmarks/results/<date>-<machine>/`.
- Where Axial is slower, say so and say why if known (for example, thread-pool fibers versus a dedicated
  work-stealing scheduler). The page is only credible if it reports losses.
- A "reproduce this" block with the exact commands.

## Order of work

1. Harness: `Axial.Comparative` with Pingpong and Fork, both libraries, FIO `Direct` and `WorkStealing`; check the
   CSVs feed `plot.py` and `compare.py`.
2. Message-passing set: Threadring, Bang, Big, Counting, BoundedBuffer.
3. Parallel set: Fibonacci, Trapezoidal, ZipRace.
4. Arbitration set: Philosophers (both variants), Chameneos.
5. The performance page, from one full recorded run.

Related: an `Axial.Hosting.AspNetCore` benchmark (plaintext and JSON handlers, raw minimal API versus the same
handler as a `Flow`) to answer the HTTP throughput question these suites do not.

Sources: the FIO repository README and `benchmarks/FIO.Benchmarks/README.md` (workloads, parameters, runtimes,
scripts), read 2026-10-20. Verify each workload's source before porting; the README is a summary.
