# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

## Integration follow-up

- `Reified.*` packages are now on NuGet (latest 0.8.2; `Directory.Build.props` pins `ReifiedVersion` 0.7.0). Restore and test `Axial.Hosting.AspNetCore`, `Axial.Hosting.GenHttp`, their examples/tests, and `examples/Axial.ReferenceApp` against package references.
- Move the cross-product reference application and host examples to a separate integration/examples repository after both release trains are public.

## Product work

- Make hand-written recursive loops (`let rec loop () = flow { ...; return! loop () }`) run in constant memory on
  .NET. Each `return!` of a pending `ValueTask` must be awaited by the caller, so every iteration stays live until
  the loop ends (~420 B per suspending iteration, ~5 B per synchronous one; measured 2026-09-26). Library loops —
  `flow { while/for }`, stream consumers and skipping operators, `Schedule.retry`/`repeat`, `Flow.Runtime.retry`/
  `supervise`, `STM.atomically`, `Queue.offerAll` — already use `Platform.loop`. Fixing user recursion needs the
  runner to trampoline binds itself; see `dev-docs/current-ideas/flow-as-data.md`. Until then, docs should steer
  long-running loops to `while`, streams, or schedules.
- Reassess remaining demand-driven Flow work in `LATER_TODO.md` against a concrete application before expanding the API.

## Acceptance

- `Axial.slnx` builds and tests without Reified.
- Core `Axial` and operational packages contain no Reified references.
- Only the isolated HTTP adapters and retained reference application cross the product boundary.
- Package, AOT, Fable, documentation, and site checks pass at release boundaries.
