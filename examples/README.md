# Examples

## Application Hosting

The hosting examples are independent applications with local run instructions in each directory:

- [`Axial.App.Example`](./Axial.App.Example/) — portable finite application using `App.run` only.
- [`Axial.Hosting.DotNet`](./Axial.Hosting.DotNet/) — standalone .NET process with Ctrl+C and exit codes.
- [`Axial.Hosting.GenericHost`](./Axial.Hosting.GenericHost/) — Microsoft Generic Host and dependency injection.
- [`Axial.Hosting.Desktop`](./Axial.Hosting.Desktop/) — desktop-framework-owned start and asynchronous close.
- [`Axial.Hosting.Node`](./Axial.Hosting.Node/) — Fable on Node with arguments, `process.env`, and signals.
- [`Axial.Hosting.Browser`](./Axial.Hosting.Browser/) — Fable browser mount with `AbortSignal` ownership.

Start with the README inside the variant matching the application host. Each example references the source projects
in this repository; package consumers can replace those `ProjectReference` entries with the corresponding Axial
NuGet package.

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

Introductory reference app (Axial.ErrorHandling only — checks, `result {}`, `refine {}`, `validate {}`):

```bash
dotnet run --project examples/Axial.ReferenceApp.Intro/Axial.ReferenceApp.Intro.fsproj --nologo
```

Reference architecture app (CLI, schema-driven ASP.NET endpoints, versioned local files, refined domain types, and Flow):

```bash
dotnet run --project examples/Axial.ReferenceApp/Axial.ReferenceApp.fsproj -- create-workspace Delivery
dotnet run --project examples/Axial.ReferenceApp/Axial.ReferenceApp.fsproj -- web --urls http://localhost:5080
```

Generated-wire reference slice (`[<DeriveSchema>]` records, contract migration, head-version codec):

```bash
dotnet run --project examples/Axial.ReferenceApp.Wire/Axial.ReferenceApp.Wire.fsproj --nologo
```

GenHTTP API twin (the Axial.Api boundary contract on a different host):

```bash
AXIAL_EXAMPLE=smoke dotnet run --project examples/Axial.Api.GenHttp/Axial.Api.GenHttp.fsproj --nologo
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

- `POST /signups` parses the JSON body through the schema (`Data.ofJsonDocument` + `Schema.parse`); invalid input
  gets a 400 with path diagnostics, valid input becomes a trusted model serialized back through the compiled
  `Axial.Schema.Json` JSON codec
- `GET /openapi.json` serves an OpenAPI document whose request schema comes from `JsonSchema.generate`
- `GET /signup` renders an HTML form from `Inspect` metadata, and `POST /signup` redisplays submitted values next to
  their errors

Set `AXIAL_EXAMPLE=smoke` to start the server on an ephemeral port, exercise every endpoint, and exit; CI runs that
mode on every push.

## Introductory Reference App

The intro app in [`examples/Axial.ReferenceApp.Intro`](./Axial.ReferenceApp.Intro/) is the first reference tier:
a conference registration desk using only `Axial.ErrorHandling`. It shows reusable checks with your own error
union, fail-fast `result {}` pipelines, refined domain values through `refine {}`, and accumulated form
validation with named diagnostics through `validate {}` — with no schemas and no Flow.

## Reference Architecture App

The reference app in [`examples/Axial.ReferenceApp`](./Axial.ReferenceApp/) shows how the pieces behave across a
non-trivial domain. It includes related schemas, refined fields and smart constructors, application admission,
v1-to-v2 contract migration, latest-version JSON persistence, Flow-based CRUD/task workflows, explicit platform GUID
generation, CLI commands, and JSON/form endpoints built through `Axial.Schema.Http.AspNetCore`. Its README explains the
current boundary and domain split. The same schemas also compile response and persistence codecs, generate OpenAPI,
drive HTML form inspection and invalid-input redisplay, while Flow composes `BaseRuntime` and the explicit filesystem
service with application persistence.

## Generated-Wire Reference Slice

The wire slice in [`examples/Axial.ReferenceApp.Wire`](./Axial.ReferenceApp.Wire/) dogfoods the contract
generator: `[<DeriveSchema>]` records produce the schemas, parse functions, typed field references, and the
versioned-contract builder, while the hand-written parts shrink to the v1 → v2 migration, a strict domain
mapping, and a head-version codec write.

## GenHTTP API Twin

The GenHTTP sample in [`examples/Axial.Api.GenHttp`](./Axial.Api.GenHttp/) serves the same schema-driven
boundary as `Axial.Api` — schema-parsed requests, problem-details 400s, codec 201s, and assembled OpenAPI —
from GenHTTP instead of ASP.NET Core, proving the boundary contract is host-neutral. Run the self-contained
pass with `AXIAL_EXAMPLE=smoke`.

## Flow Comparisons

The comparisons library in [`examples/Axial.Flow.Comparisons`](./Axial.Flow.Comparisons/) implements seven
scenarios twice — once with ordinary `Task`/exception/token code and once with Flow — over identical domain
types, with failure-path tests for every claimed guarantee in
[`tests/Axial.Flow.Comparisons.Tests`](../tests/Axial.Flow.Comparisons.Tests/). It exercises core Flow plus
the HttpClient, FileSystem, Console, Process, PlatformService, and Telemetry packages.

## Smallest Docs-First Examples

If you want the smallest possible snippets rather than runnable projects, read:

- [`docs/TINY_EXAMPLES.md`](../docs/TINY_EXAMPLES.md)
- [`docs/FSTOOLKIT_MIGRATION.md`](../docs/FSTOOLKIT_MIGRATION.md)

## Next

If you want the smallest introduction, read [`docs/GETTING_STARTED.md`](../docs/GETTING_STARTED.md).
For task and async boundary shapes, read [`docs/TASK_ASYNC_INTEROP.md`](../docs/TASK_ASYNC_INTEROP.md).
