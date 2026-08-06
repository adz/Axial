# Axial Tasks

Work this queue from top to bottom. Remove completed items rather than retaining a history here.

## Integration follow-up

- After the first `Reified.*` packages are available, restore and test `Axial.Hosting.AspNetCore`, `Axial.Hosting.GenHttp`, their examples/tests, and `examples/Axial.ReferenceApp` against package references.
- Move the cross-product reference application and host examples to a separate integration/examples repository after both release trains are public.

## Product work

- Reassess remaining demand-driven Flow work in `LATER_TODO.md` against a concrete application before expanding the API.
- Refresh `dev-docs/API_BASELINE.md` after the final package rename and record the validated commands and counts.

## Acceptance

- `Axial.slnx` builds and tests without Reified.
- Core `Axial` and operational packages contain no Reified references.
- Only the isolated HTTP adapters and retained reference application cross the product boundary.
- Package, AOT, Fable, documentation, and site checks pass at release boundaries.
