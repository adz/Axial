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

The probe was a throwaway console program; the first run of each form includes JIT and machine-load noise.
