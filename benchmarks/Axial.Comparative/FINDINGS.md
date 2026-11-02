# Findings

## `Flow.sequencePar` inline-starts synchronous CPU branches

Measured on an 8-core host (`Environment.ProcessorCount = 8`), Release build, Axial at this branch. Eight flows each
defer a loop of 100,000,000 integer add/modulo operations (`Flow.delay (fun () -> Flow.succeed (work ()))`). Four
forms were timed, three runs each, wall clock in milliseconds:

| Form | Run 1 | Run 2 | Run 3 |
|---|---:|---:|---:|
| One flow running the eight loops serially | 1677 | 1463 | 1079 |
| Eight `Task.Run` calls and `Task.WhenAll` (reference) | 372 | 367 | 316 |
| `Flow.sequencePar` over the eight flows | 1094 | 1060 | 995 |
| `Flow.fork` each flow, then `Fiber.join` each fiber | 324 | 317 | 338 |

`Flow.fork` runs the slices in parallel, as fast as `Task.Run`. `Flow.sequencePar` runs them one after another, as
slowly as the serial loop: `Platform.zipParExecution` starts each branch inline, and a branch that never suspends
finishes before the next one starts. The same applies to `Flow.zipPar`, which shares that path. Flows that suspend
(on a queue, a timer or I/O) do overlap, so the message-passing workloads are unaffected.

The comparative Trapezoidal workload therefore forks every slice and joins every fiber, which is what FIO's
`collectAllPar` does. Whether `zipPar`/`sequencePar` should start branches on the thread pool is a library question,
recorded for the maintainer; the benchmark does not depend on the answer.

A later idle-machine run with the same eight-slice shape measured `Flow.sequencePar` at 930 ms, `Task.Run` /
`Task.WhenAll` at 235 ms, `Flow.fork` at 245 ms, and `Flow.sequencePar` over `Flow.fromBlocking` at 240 ms. This
confirms that the difference is branch placement rather than the parallel result-combination path.

`Flow.sequencePar` and `Flow.zipPar` deliberately retain inline start and their allocation-free synchronous fast
path. Wrap CPU-bound branches in `Flow.runOn RunOn.ThreadPool`, or use a parallel traversal with `Parallelism.cpu`, when they should use multiple
cores. `RunOn.Scheduler` selects a custom .NET scheduler, while `RunOn.DedicatedThread` dedicates a thread only to
the synchronous prefix before the first incomplete await. Use `Flow.fromBlocking` for a blocking foreign call: it
also starts on the pool, but once started it runs to completion and the flow waits for it after interruption.

The probe was a throwaway console program; the first run of each form includes JIT and machine-load noise.
