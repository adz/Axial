# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

## Integration follow-up

- `Reified.*` packages are now on NuGet (latest 0.8.2; `Directory.Build.props` pins `ReifiedVersion` 0.7.0). Restore and test `Axial.Hosting.AspNetCore`, `Axial.Hosting.GenHttp`, their examples/tests, and `examples/Axial.ReferenceApp` against package references.
- Move the cross-product reference application and host examples to a separate integration/examples repository after both release trains are public.

## Product work

- Build `Hub<'a>` with per-subscriber strategies on top of `Queue` (Part 2 of `dev-docs/queues_and_hubs.md`).
- Add `Schedule.union`, `Schedule.intersect`, and `Schedule.fixed` (Part 3 of `dev-docs/queues_and_hubs.md`).
- Fix `Semaphore.withPermit` losing a permit when a suspended acquirer is interrupted: its signal stays in
  `PermitQueue.Waiters`, and the next `release` hands the permit to the dead waiter. Use the `Queue` waiter pattern
  (state changed under the gate; an interrupted waiter that was already granted a permit releases it).
- Make `Ref.make` and `Deferred.make` allocate per run. `Ref.make value` is `Flow.ok (Ref(...))`, so the cell is created
  when the flow value is built and every run of that value shares one cell.
- FsLiveDocs 0.7.3 bundles its own `Axial.dll` (0.9.1) and runs doc transcripts in-process, so examples that use newer
  Axial APIs (`Queue`, `FlowStream.chunkBySize`, `mapFlowPar`) fail with type-load errors. Fix in FsLiveDocs by
  isolating the evaluated assemblies from the tool's own dependencies.
- Make unbounded recursive loops run in constant memory. `Platform.guardStack` continues on a fresh stack every 96
  synchronous steps with `Task.Run`, and each hop's proxy task awaits the next, so a `let rec loop () = flow { ...;
  return! loop () }` or a long stream pull retains ~5–9 B per iteration until the loop ends (measured 2026-09-26 over
  1M iterations). Long-lived Queue/Hub consumers and control loops need a trampoline that does not chain tasks;
  see `dev-docs/current-ideas/flow-as-data.md` for the run-loop direction.
- Reassess remaining demand-driven Flow work in `LATER_TODO.md` against a concrete application before expanding the API.

## Acceptance

- `Axial.slnx` builds and tests without Reified.
- Core `Axial` and operational packages contain no Reified references.
- Only the isolated HTTP adapters and retained reference application cross the product boundary.
- Package, AOT, Fable, documentation, and site checks pass at release boundaries.
