# Examples

This page points to runnable examples so you can see what the Axial package family looks like in code.

## Run The Examples

Main example:

```bash
dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo
```

Readme example:

```bash
dotnet run --project examples/Axial.ReadmeExample/Axial.ReadmeExample.fsproj --nologo
```

Full source:

[`examples/Axial.ReadmeExample/Program.fs`](./Axial.ReadmeExample/Program.fs)

Maintenance example:

```bash
dotnet run --project examples/Axial.MaintenanceExamples/Axial.MaintenanceExamples.fsproj --nologo
```

Playground example:

```bash
dotnet run --project examples/Axial.Playground/Axial.Playground.fsproj --nologo
```

Minimal API sample (browse to /signup, or run the smoke pass below):

```bash
dotnet run --project examples/Axial.Api/Axial.Api.fsproj
AXIAL_EXAMPLE=smoke dotnet run --project examples/Axial.Api/Axial.Api.fsproj --nologo
```

Reference architecture app (CLI, HTML forms, JSON API, versioned local files, schemas, refined domain types, and Flow):

```bash
dotnet run --project examples/Axial.ReferenceApp/Axial.ReferenceApp.fsproj -- create-workspace Delivery
dotnet run --project examples/Axial.ReferenceApp/Axial.ReferenceApp.fsproj -- web --urls http://localhost:5080
```

NativeAOT probe:

```bash
bash scripts/run-aot-probe.sh
```

## Main Example

The main example in [`examples/Axial.Examples/Program.fs`](./Axial.Examples/Program.fs) shows a small application-shaped set of flows:

- validate configuration with plain `Result`
- build a smaller runtime environment from config
- call a `Task<Result<_,_>>` dependency
- retry transient failures
- apply a timeout
- persist an audit record through a `Task` boundary
- scope an async resource with `use`
- compose smaller flows into a larger config-driven flow with `Flow.localEnv`

Read it in this order:

1. `validateConfig`
2. `fetchResponse`
3. `saveAudit`
4. `program`

## Maintenance Example

The maintenance example in [`examples/Axial.MaintenanceExamples/Program.fs`](./Axial.MaintenanceExamples/Program.fs) is smaller and more focused. It shows:

- how to normalize awkward nested wrapper shapes one layer at a time
- the difference between cold task factories and already-created task values

## Playground Example

The playground example in [`examples/Axial.Playground/Program.fs`](./Axial.Playground/Program.fs) is the quickest way to feel the new surface in practice. It shows:

- plain `Result` validation first
- a small `flow {}` workflow
- projected environment reads through `Flow.read`
- one `.NET` boundary through `taskFlow {}`

## Minimal API Sample

The API sample in [`examples/Axial.Api/Program.fs`](./Axial.Api/Program.fs) is a complete ASP.NET Core minimal API
where one schema declaration drives everything at the boundary:

- `POST /signups` parses the JSON body through the schema (`RawInput.ofJsonDocument` + `Input.parse`); invalid input
  gets a 400 with path diagnostics, valid input becomes a trusted model serialized back through the compiled
  `Axial.Codec` JSON codec
- `GET /openapi.json` serves an OpenAPI document whose request schema comes from `JsonSchema.generate`
- `GET /signup` renders an HTML form from `Inspect` metadata, and `POST /signup` redisplays submitted values next to
  their errors

Set `AXIAL_EXAMPLE=smoke` to start the server on an ephemeral port, exercise every endpoint, and exit; CI runs that
mode on every push.

## Reference Architecture App

The reference app in [`examples/Axial.ReferenceApp`](./Axial.ReferenceApp/) shows how the pieces behave across a
non-trivial domain. It includes four related schemas, refined fields and smart constructors, contextual rules,
v1-to-v2 contract migration, latest-version JSON persistence, Flow-based CRUD/task workflows, CLI commands, an HTML
form, a JSON API, and focused architecture tests. Its README also records the wrapper friction the example exposes.

## Smallest Docs-First Examples

If you want the smallest possible snippets rather than runnable projects, read:

- [`docs/TINY_EXAMPLES.md`](../docs/TINY_EXAMPLES.md)
- [`docs/FSTOOLKIT_MIGRATION.md`](../docs/FSTOOLKIT_MIGRATION.md)

## Next

If you want the smallest introduction, read [`docs/GETTING_STARTED.md`](../docs/GETTING_STARTED.md).
For task and async boundary shapes, read [`docs/TASK_ASYNC_INTEROP.md`](../docs/TASK_ASYNC_INTEROP.md).
