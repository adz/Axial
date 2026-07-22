# Axial reference application

This page shows Axial's boundary and workflow packages cooperating in one application rather than as isolated API
samples.

The application tracks workspaces, members, and work items. It includes a CLI, JSON and HTML boundaries, file
persistence, v1-to-v2 migration, business transitions, and tests. The breadth matters: API friction that disappears in
a single validation snippet becomes visible when values cross several boundaries.

## Run it

### Run with the Aspire dashboard

The single-file [`apphost.cs`](apphost.cs) starts the web application and Aspire dashboard without a separate AppHost
project or Aspire installation. It requires only the .NET 10 SDK. The `#:sdk` directive restores the Aspire AppHost
SDK from NuGet on the first run.

From this directory, run:

```bash
dotnet run --file apphost.cs
```

Open the dashboard at `http://localhost:18888`, then select the `reference-app` resource and its `http` endpoint. The
sample binds its local dashboard and OTLP endpoints without authentication; keep them on localhost.

Call the demonstration endpoint once to populate every observability view:

```bash
curl http://localhost:5080/observability/demo
```

The dashboard then shows:

- **Traces:** an ASP.NET Core request span containing `observability.demo`, plus `demo-fast` and `demo-slow` named
  fiber spans. The Flow span carries the `axial.flow.annotation.demo.kind` attribute and a fiber-dump event.
- **Structured logs:** the `Running observability demo` entry has `DemoKind` and `ExpectedFibers` properties. Messages
  emitted inside the workflow through `Log.info` use the `Axial.ReferenceApp.Flow` category and correlate through the
  active trace and span IDs.
- **Metrics:** select the `Axial.Flow` meter to inspect fibers started, live, settled, duration, and unobserved defects.

Ordinary API requests use the same instrumentation. For example, an invalid import demonstrates a typed Flow failure
on the `workspaces.import` span:

```bash
curl -i http://localhost:5080/api/workspaces \
  -H 'content-type: application/json' \
  --data '{}'
```

Press Ctrl+C in the terminal to stop the application and dashboard.

### Run without Aspire

The CLI and web application still run directly:

```bash
dotnet run --project examples/Axial.ReferenceApp/Axial.ReferenceApp.fsproj -- create-workspace Delivery
dotnet run --project examples/Axial.ReferenceApp/Axial.ReferenceApp.fsproj -- list
dotnet run --project examples/Axial.ReferenceApp/Axial.ReferenceApp.fsproj -- web --urls http://localhost:5080
```

When running the web host directly, OpenTelemetry uses the standard `OTEL_EXPORTER_OTLP_ENDPOINT` environment
variable if you want to send signals to another OTLP backend.

Data is written to `.axial-reference-data` unless `AXIAL_REFERENCE_DATA` names another directory.

## Layout

1. `Domain.fs` defines private refined values and business transitions.
2. `Contracts.fs` defines schemas, version migration, fallible domain mapping, and production admission.
3. `Application.fs` defines persistence and Flow use cases over an explicit store, `BaseRuntime`, and `IFileSystem`.
4. `Program.fs` adapts CLI commands, configures OpenTelemetry, and uses `Axial.Schema.Http.AspNetCore` for routes, JSON,
   forms, endpoint Flow, problem details, compiled responses, schema-derived OpenAPI, inspection, and retained-input
   redisplay.
5. `apphost.cs` is the single-file Aspire orchestration entry point.
6. `tests/Axial.ReferenceApp.Tests` covers parsing, migration, admission, transitions, persistence, and HTTP.

## Construction and trust

`WorkspaceV1`, `WorkspaceV2`, `MemberV2`, and `WorkItemV2` are wire records. They remain easy to construct because
their purpose is representation. `Schema.parse` establishes boundary trust for one operation; it does not change the
constructors of those record types.

`WorkspaceName`, `PersonName`, and `WorkItemTitle` have private representations and fallible smart constructors. Their
schemas use the same constructors through `Schema.refine`:

```fsharp
let private requiredText refine inspect maximum =
    Schema.text
    |> Schema.constrainAll [ Constraint.required; Constraint.maxLength maximum ]
    |> Schema.refine refine SchemaError.ofRefinementError inspect
```

The raw constraints supply portable metadata and standard boundary diagnostics. The smart constructor supplies the
durable domain invariant. There is no unchecked constructor justified by an assumption that constraints ran first.

`Workspace` is the business model. Code maps into it once after schema parsing and operation-specific admission, then
uses the bare domain value. The mapping is fallible because member/assignee consistency is an application invariant
that a wire schema cannot establish. Persisted data passes through the same schema and domain mapping on read because
storage is an external construction path.

## One schema catalog

Primitive, collection, refined, and record declarations all have type `Schema<_>`:

```fsharp
Schema.text
Schema.list<string>()
Schema.map<int>()
RefinedSchemas.nonBlankString
workspaceV2
```

Record fields infer built-ins and canonical type schemas. `fieldWith` remains for the deliberately local refined and
nested schemas in this application:

```fsharp
Schema.define<WorkspaceV2>
|> fieldWith workspaceName "name" _.name
|> fieldWith (Schema.listWith memberV2) "members" _.members
|> construct constructor
```

`Axial.Schema.Syntax` supplies the constructor-last shape operations. Value schemas remain qualified under `Schema`.

## Rules at the right level

Three kinds of rule appear in the application:

- non-blank important text is intrinsic and belongs in private refined types;
- wire length, requiredness, and state tags belong in schemas;
- “production names cannot end in `-demo`” and “assignees must be members” depend on admission context and use
  an ordinary result-returning application function.

Putting all three into one validator would either weaken domain construction or make a reusable schema depend on a
specific workflow.

## Integration boundaries

`Data` handles untrusted source-neutral data. `Contract` selects and migrates persisted versions. `Axial.Schema.Json`
handles trusted current-version JSON for storage, HTTP responses, and CLI output. `Axial.Schema.Http` and its ASP.NET
Core adapter parse route, JSON, and form input before embedding application Flow into native endpoints. The same schema
catalog generates `/openapi.json`; `Inspect` renders the new-workspace form; `RetainedParseResult` redisplays invalid
form values beside their path-specific diagnostics.

`Check` and `Result` express path-free admission and domain transitions. `Flow` then orchestrates those fallible values
with the workspace store. The file adapter performs its operational work through `Axial.Flow.FileSystem`, so filesystem
failures enter the typed application error channel instead of escaping from ambient `System.IO`. `BaseRuntime.liveValue`
supplies the standard live clock, logging, randomness, GUID, and environment-variable services as one bundle; tests
replace only the GUID service when deterministic identifiers matter.

That separation is the point of the app: Schema proves boundary shape, refined types retain intrinsic guarantees,
ordinary Result values admit domain relationships, and Flow coordinates explicit effects. No layer substitutes for the
next one.

The file store uses temporary-file replacement, but it is not suitable for concurrent processes or large datasets.
Use a database adapter with the same `IWorkspaceStore` boundary for those scenarios.
