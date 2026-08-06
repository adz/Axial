# Axial Plan

Axial is an asynchronous F# workflow library. Its public model is `Flow<'env, 'error, 'value>` with shorter aliases for common channel combinations. Dependencies are explicit in `'env`; typed expected failures are explicit in `'error`; executor mechanics remain in the closed runtime.

## Package direction

- `Axial` — workflows, exits, causes, fibers, structured concurrency, schedules, streams, STM, layers, and application lifetime
- `Axial.PlatformService` — explicit clock, logging, randomness, GUID, and environment services
- `Axial.Console`, `Axial.FileSystem`, `Axial.HttpClient`, `Axial.Process` — mockable operational services
- `Axial.Hosting`, `Axial.Hosting.Node`, `Axial.Hosting.Browser` — application lifecycle integrations
- `Axial.Telemetry`, `Axial.Telemetry.JavaScript` — tracing and runtime observability
- `Axial.Hosting.AspNetCore`, `Axial.Hosting.GenHttp` — optional adapters that execute Reified HTTP contracts

Core `Axial` has no Reified dependency. Host adapters are the only library projects allowed to reference Reified. The retained reference application is integration evidence, not part of the core product; it should move to a separate examples repository when both package families are published.

## Architecture

- Application and operational dependencies are explicit services in `'env`.
- Ambient runtime state is limited to cancellation, scopes, scheduling, interruption, annotations, and trace mechanics.
- Process and HTTP implementations inject clocks and other additional effects explicitly.
- `App` owns a root workflow lifetime; host packages translate native lifecycle events at the edge.
- The core remains reflection-free, trimming-safe, AOT-friendly, and Fable-compatible.
- Pre-1.0 APIs are replaced directly; do not preserve obsolete aliases.

## Documentation

Teach from the smallest useful `Flow` shape, then introduce environment and typed failure channels. The public site is Flow-only. Reified appears only where an integration example genuinely crosses the repository boundary.

Durable decisions remain in `dev-docs/decisions/README.md`; current work belongs in `dev-docs/TASKS.md`.
