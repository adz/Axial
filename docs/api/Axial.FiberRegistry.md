# FiberRegistry

A `FiberRegistry` (core `Axial`, no telemetry dependency) tracks every live fiber below one edge install and answers
"what is my runtime doing right now?" Install it with `Flow.withFiberRegistry`; `registry.DumpAt(clock)` renders the
tree and `registry.Snapshot()` returns structured `FiberDump` values.

## Method inventory

- `registry.Settled()` returns the most recently settled fibers, oldest first. Each carries its final metadata
  (status, start and settle times, `Duration`) and, for a failed fiber, its cause rendered as text, for typed errors
  as well as defects.
- `registry.Stats()` returns totals per fiber name since the registry was installed: how many ran, failed, and were
  interrupted, with total and longest duration. Unnamed fibers are counted under `(unnamed)`.
- `registry.UnobservedDefects()` returns defects that nobody joined or awaited.
- `registry.StartedCount` counts every fiber started.
- `registry.Interrupt id` signals one live fiber to stop, and `registry.InterruptByName "name"` signals every live
  fiber with that name and returns how many it signalled. Neither waits; whoever joins or awaits the fiber sees
  `Cause.Interrupt`.

History is bounded: `FiberRegistry()` keeps the last 200 settled fibers and unobserved defects, and
`FiberRegistry(capacity)` sets another bound. Totals are kept for every name. Install the registry with
`Flow.withFiberRegistry` to get typed failure text; an observer composed by hand sees defects only.

Read the [observability guide](/observability/index.html) for the operational interpretation and
[Supervision and fiber observability](/concurrency-and-state/supervision.html) for unobserved-defect semantics.
