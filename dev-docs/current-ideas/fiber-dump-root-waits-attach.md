# Fiber dumps: the root fiber, what each fiber waits on, and attaching late

Status: accepted direction (owner, 2026-10-28), queued after the guardrails merge. Needs benchmarks before merge.

## Problem

- `FiberRegistry` only sees fibers forked after `Flow.withFiberRegistry` installs it. The root run is never shown, so a
  hang in the main flow dumps as "0 live fibers", children point at a parent the dump never shows, and
  `registry.Interrupt` cannot reach the stuck root.
- A dump says a fiber is `Running` but not what it is waiting on. `race`/`zipPar` branches are not forked fibers, so a
  fiber stuck in a race shows nothing more.
- A registry cannot be turned on in an already-running application: running fibers copied their runtime context (and
  observer) when they were forked.

## What already exists

Every fork already writes to a process-wide live table (`FiberInterrupts`: fiber id → interrupt source, global lock,
add on fork, remove on settle), and every fork already allocates a `FiberMetadata`. The root run already has a
`FiberId` (children record it as `ParentId`); it only lacks metadata and a start event.

## Design

1. **Live table carries metadata.** `FiberInterrupts` stores `(metadata, interrupt source)` instead of the source alone,
   and metadata gains `RootId` so one process with several runs (tests) can be filtered per run. No new allocation per
   fork; the table write already happens.
2. **Root entry, lazily.** The first fork from a root (or `withFiberRegistry` in it) registers the root's metadata in the
   live table; it is removed when the run ends. Runs that never fork pay nothing. Benchmark the eager alternative too.
3. **Attach late.** A dump can be taken at any time from the live table: `registry.DumpAt` (or a static live snapshot)
   reads it, so installing a registry later still shows every live fiber, the root, and their waits. Settled history
   and per-name stats still need the observer, so they cover only the time since installation; say so in the docs.
4. **Waiting on.** Metadata gains a mutable `WaitingOn` (a static description, no allocation: e.g.
   `Deferred.await`, `Fiber.awaitExit #12`, `Queue.take "jobs"`, `sleep until +3.2s`, `race (2 branches)`, `Task`).
   Each wait primitive sets it on its suspend path and clears it on resume. Synchronous fast paths do not touch it.
   The dump renders it: `#4 "owner" Waiting on Deferred.await 10.1s`.

## Hot path and benchmarks

- Fork: unchanged work (metadata object and table write already exist) plus one field.
- Suspend/resume: one field store each, only when actually suspending; the current fiber's metadata is found through
  the runtime state the suspend path already reads. Measure with FiberCost, ParallelComposition, Flow bind, Race,
  Pingpong/Threadring (the queue hand-off path), and a tiny `runSync` loop for the root entry. Allocation must be
  identical; time within noise.
- The global lock in the live table is existing contention; if the benchmarks show it, stripe it or use a concurrent
  map as part of this slice.

## Tests

- A root blocked on a Deferred with no forks dumps as one live fiber waiting on `Deferred.await`.
- A registry installed after fibers started still dumps them, with parents and waits.
- `registry.Interrupt` reaches the root.
- Torture: the watchdog (examples/Axial.TortureTest/Program.fs) shows waits in its stall dump.
