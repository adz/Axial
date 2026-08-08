# Axial Agent Index

Read this after `AGENTS.md` and before broad repository search.

## Product boundary

Axial is the workflow product. Core is `src/Axial`; focused operational and hosting packages are the other `src/Axial.*` directories. Reified owns constraints, refinements, parsing, Result helpers, Data, Schema, codecs, and contracts in a separate repository.

`src/Axial.Hosting.AspNetCore` and `src/Axial.Hosting.GenHttp` are explicit cross-product adapters. `examples/Axial.ReferenceApp` is retained temporarily as the integration example. They consume Reified packages and are intentionally excluded from `Axial.slnx` until those packages are published; core Axial must never depend on Reified.

## Task routing

- Core workflow/runtime: `src/Axial/**`, `tests/Axial.Tests/**`
- Operational services: matching `src/Axial.{Console,FileSystem,HttpClient,Process,PlatformService}` and test projects
- Hosting: `src/Axial.Hosting*`, `tests/Axial.Hosting.Tests/**`
- Telemetry: `src/Axial.Telemetry*`, `tests/Axial.Telemetry.Tests/**`
- User documentation: `docs/**`; read `dev-docs/DOCS.md` first
- Architecture and queue: `dev-docs/PLAN.md`, `dev-docs/TASKS.md`, `dev-docs/decisions/README.md`

## Generated paths

`.livedocs/build-history/**`, `output/**`, `artifacts/**`, `**/bin/**`, and `**/obj/**` are generated and must remain untracked.

## Validation

- `bash scripts/check-source-inventory.sh`
- `dotnet build Axial.slnx --nologo -v minimal`
- `dotnet test Axial.slnx --nologo -v minimal`
- `bash scripts/run-aot-probe.sh`
- `bash scripts/check-fable-js-surface.sh`
- `bash scripts/validate-docs.sh`
