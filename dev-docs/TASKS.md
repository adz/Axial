# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

## Integration follow-up

- `Reified.*` packages are now on NuGet (latest 0.8.2; `Directory.Build.props` pins `ReifiedVersion` 0.7.0). Restore and test `Axial.Hosting.AspNetCore`, `Axial.Hosting.GenHttp`, their examples/tests, and `examples/Axial.ReferenceApp` against package references.
- Move the cross-product reference application and host examples to a separate integration/examples repository after both release trains are public.

## Product work

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
