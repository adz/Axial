# Axial v1 Baseline and API Surface Policy

This file records the current v1 stabilization baseline. It is for maintainers and coding agents, not user-facing
documentation.

## Current Baseline

- Recorded: `2026-07-05T12:57:15Z`
- Baseline commit before this update: `95e95c97`
- .NET SDK: `10.0.300`
- Node.js used locally: `v26.1.0`

Note (2026-07-12): the validated-command record below is historical — it predates the 2026-07-09..13 renames and
references test projects that have since been restructured (`Axial.Refined.Tests` and `Axial.Validation.Tests`
folded into `Axial.ErrorHandling.Tests`). The project lists in this file were corrected on 2026-07-12; a fresh
validated-command pass is queued in `dev-docs/TASKS.md`.

Validated commands for this refresh:

```text
bash scripts/check-source-inventory.sh
=> Source inventory covers src/tests .fs and .fsproj files.

dotnet build tests/Axial.ApiShape.Tests/Axial.ApiShape.Tests.fsproj --no-restore --nologo -v minimal
dotnet build tests/Axial.ErrorHandling.Tests/Axial.ErrorHandling.Tests.fsproj --no-restore --nologo -v minimal
dotnet build tests/Axial.Flow.FileSystem.Tests/Axial.Flow.FileSystem.Tests.fsproj --no-restore --nologo -v minimal
dotnet build tests/Axial.Flow.Hosting.Tests/Axial.Flow.Hosting.Tests.fsproj --no-restore --nologo -v minimal
dotnet build tests/Axial.Flow.Integration.Tests/Axial.Flow.Integration.Tests.fsproj --no-restore --nologo -v minimal
dotnet build tests/Axial.Flow.PlatformService.Tests/Axial.Flow.PlatformService.Tests.fsproj --no-restore --nologo -v minimal
dotnet build tests/Axial.Flow.Telemetry.Tests/Axial.Flow.Telemetry.Tests.fsproj --no-restore --nologo -v minimal
dotnet build tests/Axial.Flow.Tests/Axial.Flow.Tests.fsproj --no-restore --nologo -v minimal
dotnet build tests/Axial.Refined.Tests/Axial.Refined.Tests.fsproj --no-restore --nologo -v minimal
dotnet build tests/Axial.Schema.Tests/Axial.Schema.Tests.fsproj --no-restore --nologo -v minimal
dotnet build tests/Axial.Schema.Tests/Axial.Schema.Tests.fsproj --no-restore --nologo -v minimal
dotnet build tests/Axial.Validation.Tests/Axial.Validation.Tests.fsproj --no-restore --nologo -v minimal
=> Build succeeded for each package-boundary test project.

bash scripts/run-aot-probe.sh
=> Exit code 0.
```

Additional validated commands for this refresh:

```text
dotnet test tests/Axial.ApiShape.Tests --nologo (36 passed)
dotnet test tests/Axial.Schema.Tests --nologo (56 passed)
dotnet test tests/Axial.Codec.Tests --nologo (13 passed)
dotnet test tests/Axial.Schema.Tests --nologo (stale count; re-baseline after the 2026-07 renames)
dotnet test tests/Axial.Flow.Tests --nologo (89 passed)
bash scripts/validate-docs.sh
=> Docs validation build succeeded, including regenerated reference docs.
```

Known validation gaps observed during this refresh: none. The previous Fable gaps are fixed:
`benchmarks/Axial.Benchmarks.Fable` now compiles `Predicate.fs` before `Check.fs`, and
`Value.inspectUnderlying` guards its .NET-only generic projection-type validation with `#if !FABLE_COMPILER`, so
`dotnet build Axial.slnx` and `bash scripts/check-fable-js-surface.sh` both pass.

The full solution build, generated API docs, docs preview, production site build, and an unrestricted `dotnet test` run
are required before committing any release/API-surface update. Record their result in the commit summary when they are
run.

## Package-Boundary Test Projects

The old monolithic `tests/Axial.Tests/Axial.Tests.fsproj` harness has been replaced by package-boundary test projects:

- `tests/Axial.ApiShape.Tests/Axial.ApiShape.Tests.fsproj`
- `tests/Axial.Codec.Tests/Axial.Codec.Tests.fsproj`
- `tests/Axial.ErrorHandling.Tests/Axial.ErrorHandling.Tests.fsproj`
- `tests/Axial.Flow.FileSystem.Tests/Axial.Flow.FileSystem.Tests.fsproj`
- `tests/Axial.Flow.Hosting.Tests/Axial.Flow.Hosting.Tests.fsproj`
- `tests/Axial.Flow.HttpClient.Tests/Axial.Flow.HttpClient.Tests.fsproj`
- `tests/Axial.Flow.Integration.Tests/Axial.Flow.Integration.Tests.fsproj`
- `tests/Axial.Flow.PlatformService.Tests/Axial.Flow.PlatformService.Tests.fsproj`
- `tests/Axial.Flow.Telemetry.Tests/Axial.Flow.Telemetry.Tests.fsproj`
- `tests/Axial.Flow.Tests/Axial.Flow.Tests.fsproj`
- `tests/Axial.ReferenceApp.Tests/Axial.ReferenceApp.Tests.fsproj`
- `tests/Axial.Schema.Contracts.Tests/Axial.Schema.Contracts.Tests.fsproj`
- `tests/Axial.Schema.Testing.Tests/Axial.Schema.Testing.Tests.fsproj`
- `tests/Axial.Schema.Tests/Axial.Schema.Tests.fsproj`

## CI Baseline Gates

CI currently proves:

- every `src/**/*.fsproj` and `tests/**/*.fsproj` project is listed by `Axial.slnx`
- every `src/**/*.fs` and `tests/**/*.fs` file is explicitly compiled by a `src` or `tests` project
- the package-boundary test projects run
- `tests/Axial.ApiShape.Tests` compiles the public API surface expected by users and examples
- the intended Fable JavaScript surface compiles and excludes .NET-only `ColdTask`
- examples run
- the NativeAOT probe publishes and runs
- the core package packs
- generated API docs and the docs site build

## API Surface Policy Before 1.0

Axial is still pre-1.0, so breaking changes are allowed when they improve coherence. However, every public API change
must be deliberate.

Required checks for public API changes:

1. Update or extend `tests/Axial.ApiShape.Tests/ApiShapeTests.fs` in the same change.
2. Update XML docs on the changed public members.
3. Regenerate API docs with `bash scripts/generate-api-docs.sh`.
4. Build the docs site with `npm run build` in `site`.
5. Update `dev-docs/TASKS.md`, `dev-docs/PLAN.md`, and `RELEASE_NOTES.md` when a change affects v1 scope or release
   notes.

Public API removals and renames are acceptable before v1 only when they are intentional and reflected in:

- API-shape tests
- generated reference docs
- the relevant `dev-docs` plan/spec

After v1, compatibility aliases and deprecation windows should replace immediate removals unless a security or
correctness issue requires a hard break.

## Shape-Test Coverage

`ApiShapeTests.fs` is the current API baseline mechanism. It does not freeze every overload signature, but it must cover
the named modules, types, and members users and examples are expected to depend on:

- `Flow`, `Flow.Runtime`, `Execution`, `Cause`, `Exit`, `Fiber`, `Scope`
- computation builders
- `Check`, `Bind`, `BindError`, `Validation`, `Diagnostics`
- `Schema`, `Value`, `Field`, `SchemaConstraint`, `Inspect` and its description types, `JsonSchema`
- `Axial.Codec` `Json` module and `JsonCodec`
- `RawInput`, `Schema.parse`/`Schema.check`, `RetainedParseResult`, `SchemaError`, `ContextRules`, `FieldRef`, `Contract`
- `Policy` and `Flow.verify`
- the leaf-package dependency graph (`leaf packages stay independent of each other`)
- `Schedule`, `FlowStream`, `STM`, `TRef`, `Ref`
- `Service`, `Layer`, `LayerBuilder`
- first-party service packages
- hosting and telemetry adapter modules

If a public module is added, add it to the shape tests unless it is explicitly experimental and documented as such.
