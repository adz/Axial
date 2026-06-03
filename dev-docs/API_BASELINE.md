# FsFlow v1 Baseline and API Surface Policy

This file records the current v1 stabilization baseline. It is for maintainers and coding agents, not user-facing
documentation.

## Current Baseline

- Recorded: `2026-06-03T14:05:45Z`
- Baseline commit before this update: `7870a67f`
- .NET SDK: `10.0.300`
- Node.js used locally: `v26.1.0`

Validated commands:

```text
bash scripts/check-source-inventory.sh
=> Source inventory covers src/tests .fs and .fsproj files.

dotnet build tests/FsFlow.Tests/FsFlow.Tests.fsproj --nologo -v minimal
=> Build succeeded.

timeout 300s dotnet test tests/FsFlow.Tests/FsFlow.Tests.fsproj --no-build --nologo -v minimal
=> Passed: 130, Failed: 0, Skipped: 0.

dotnet run --project tests/FsFlow.Tests/FsFlow.Tests.fsproj --nologo
=> Exit code 0.

bash scripts/check-fable-js-surface.sh
=> Fable JavaScript surface compiles and excludes .NET-only ColdTask.

bash scripts/run-aot-probe.sh
=> Exit code 0.
```

The full solution build, generated API docs, docs preview, and production site build are required before committing any
baseline/API-surface update. Record their result in the commit summary when they are run.

## CI Baseline Gates

CI currently proves:

- every `src/**/*.fsproj` and `tests/**/*.fsproj` project is listed by `FsFlow.slnx`
- every `src/**/*.fs` and `tests/**/*.fs` file is explicitly compiled by a `src` or `tests` project
- the FsFlow test harness runs
- the intended Fable JavaScript surface compiles and excludes .NET-only `ColdTask`
- examples run
- the NativeAOT probe publishes and runs
- the core package packs
- generated API docs and the docs site build

## API Surface Policy Before 1.0

FsFlow is still pre-1.0, so breaking changes are allowed when they improve coherence. However, every public API change
must be deliberate.

Required checks for public API changes:

1. Update or extend `tests/FsFlow.Tests/ApiShapeTests.fs` in the same change.
2. Update XML docs on the changed public members.
3. Regenerate API docs with `bash scripts/generate-api-docs.sh`.
4. Build the docs site with `npm run build` in `site`.
5. Update `TODO.md` and `RELEASE_NOTES.md` when a change affects v1 scope or release notes.

Public API removals and renames are acceptable before v1 only when they are intentional and reflected in:

- API-shape tests
- generated reference docs
- `TODO.md` or the relevant `dev-docs` plan/spec

After v1, compatibility aliases and deprecation windows should replace immediate removals unless a security or
correctness issue requires a hard break.

## Shape-Test Coverage

`ApiShapeTests.fs` is the current API baseline mechanism. It does not freeze every overload signature, but it must cover
the named modules, types, and members users and examples are expected to depend on:

- `Flow`, `Flow.Runtime`, `EffectFlow`, `Cause`, `Exit`, `Fiber`, `Scope`
- computation builders
- `Check`, `Guard`, `Validation`, `Diagnostics`
- `Schedule`, `FlowStream`, `STM`, `TRef`, `Ref`
- `Service`, `Layer`, `LayerBuilder`
- first-party service packages
- hosting and telemetry adapter modules

If a public module is added, add it to the shape tests unless it is explicitly experimental and documented as such.
