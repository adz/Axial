# Axial reference application

This page shows Schema, refined domain values, versioned contracts, contextual rules, codecs, and Flow in one small application.

The application tracks workspaces, members, and work items. It includes a CLI, JSON and HTML boundaries, file
persistence, v1-to-v2 migration, business transitions, and tests. The breadth matters: API friction that disappears in
a single validation snippet becomes visible when values cross several boundaries.

## Run it

```bash
dotnet run --project examples/Axial.ReferenceApp/Axial.ReferenceApp.fsproj -- create-workspace Delivery
dotnet run --project examples/Axial.ReferenceApp/Axial.ReferenceApp.fsproj -- list
dotnet run --project examples/Axial.ReferenceApp/Axial.ReferenceApp.fsproj -- web --urls http://localhost:5080
```

Data is written to `.axial-reference-data` unless `AXIAL_REFERENCE_DATA` names another directory.

## Layout

1. `Domain.fs` defines private refined values and business transitions.
2. `Contracts.fs` defines universal schemas, version migration, domain mapping, and production-only rules.
3. `Application.fs` defines persistence and Flow use cases over an explicit environment.
4. `Program.fs` adapts CLI, forms, and JSON requests.
5. `tests/Axial.ReferenceApp.Tests` covers parsing, migration, rules, transitions, persistence, and HTTP.

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

`Workspace` is the business model. Code maps into it once after schema and contextual checks, then uses the bare
domain value. Persisted data is parsed again on read because storage is an external construction path.

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
  `ContextRules`.

Putting all three into one validator would either weaken domain construction or make a reusable schema depend on a
specific workflow.

## Integration boundaries

`Data` handles untrusted source-neutral data. `Contract` selects and migrates persisted versions. `Axial.Schema.Codec`
handles trusted current-version JSON output. `Flow` expresses store dependencies and application failures. Schema
does not replace any of those jobs.

The file store uses temporary-file replacement, but it is not suitable for concurrent processes or large datasets.
Use a database adapter with the same `IWorkspaceStore` boundary for those scenarios.

## Findings carried into the API

The earlier reference app exposed three avoidable costs:

- `Value.*`, `Schema.*`, and `Model.*` forced users to classify a declaration before using common operations;
- total refined conversion encouraged an unchecked constructor beside the real `Result`-returning constructor;
- a trust wrapper differed across parse, check, and contract APIs while private domain types already carried the
  useful durable guarantee.

The current app uses one `Schema` catalog, fallible `Schema.refine`, bare successful results, and explicit guidance
about what a schema operation proves. The remaining unwrap calls on nominal text types are real F# representation
costs; convenience functions can reduce them, but a nominal type cannot also be `System.String`.
