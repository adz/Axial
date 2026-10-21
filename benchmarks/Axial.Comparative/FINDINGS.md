# Findings

## `Flow.sequencePar` inline-starts synchronous CPU branches

Measured on this development host with `Environment.ProcessorCount = 8`. The repro created eight flows; each flow deferred a loop of 20,000,000 integer multiply/add operations. It timed three forms after building Axial in Release:

- one flow running all eight loops serially
- `Flow.sequencePar` over the eight deferred slice flows
- `Flow.fork` for every slice followed by `Fiber.join` for every fiber

The command was `dotnet fsi /tmp/zip-repro.fsx`, referencing the Release `Axial.dll`. Times in milliseconds were:

| Form | Run 1 | Run 2 | Run 3 | Median |
|---|---:|---:|---:|---:|
| Serial | 197.0 | 186.0 | 190.3 | 190.3 |
| `sequencePar` | 409.1 | 408.6 | 419.9 | 409.1 |
| Fork/join | 257.6 | 186.4 | 189.9 | 189.9 |

`sequencePar` achieved no parallel execution of these synchronous branches. `Platform.zipParExecution` starts each operation inline, and a deferred CPU-only branch does not yield before completing, so the next branch starts afterward. Fork/join permits the slices to execute as scheduled fibers. The comparative Trapezoidal workload therefore explicitly forks every slice and joins every fiber rather than using `sequencePar`.

These timings are a behavioural repro, not a general performance comparison; they include JIT, scheduler, and machine load effects.
