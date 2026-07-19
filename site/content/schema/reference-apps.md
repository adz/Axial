---
weight: 90
title: "Walkthrough: Reference Apps"
description: The reference application tiers — hand-written schemas across real boundaries, generated wire contracts, schema-derived property tests, and two HTTP hosts on one declaration.
type: docs
---


The reference apps exercise the schema group the way an application does — values crossing CLI, form, JSON, and
storage boundaries — so that API friction invisible in a snippet has nowhere to hide. There are three tiers, each
runnable:

1. [`Axial.ReferenceApp.Intro`]({{< relref "/error-handling/reference-app.md" >}}) — plain `Result`, checks,
   refined values, and accumulated validation, with no schemas at all. Start there if you are new; this page
   covers the schema tiers.
2. `examples/Axial.ReferenceApp` — the workspace tracker: hand-written schemas, refined domain values, versioned
   contracts, contextual rules, codecs, and Flow use cases.
3. `examples/Axial.ReferenceApp.Wire` — the same boundary discipline with the wire tier **generated** from
   `[<WireSchema>]` records.

## The workspace tracker

```bash
dotnet run --project examples/Axial.ReferenceApp/Axial.ReferenceApp.fsproj -- create-workspace Delivery
dotnet run --project examples/Axial.ReferenceApp/Axial.ReferenceApp.fsproj -- web --urls http://localhost:5080
```

The app tracks workspaces, members, and work items across a CLI, an HTML form, a JSON API, and versioned JSON
files. The features it leans on, and why they matter at this size:

- **One schema catalog.** Primitives, collections, refined fields, and records all have type `Schema<_>`, so
  `Schema.field name getter fieldSchema` composition is the only construction mechanism to learn
  ([DSL]({{< relref "/schema/dsl.md" >}})).
- **Refined fields call the domain's own smart constructors.** `WorkspaceName`, `PersonName`, and
  `WorkItemTitle` have private representations; `Schema.refine` runs the same fallible `create` the rest of the
  application uses, so there is no second copy of the invariant to drift
  ([Construction Guarantees]({{< relref "/schema/trusted-construction.md" >}})).
- **Versioned contracts revalidate migrations.** v1 payloads parse through the frozen v1 schema, migrate through
  hand-written typed functions, and the migrated output is re-checked against the current schema — migration code
  cannot quietly produce invalid current values ([Versioned Contracts]({{< relref "/schema/contracts.md" >}})).
- **One declaration, several interpreters.** The same schemas drive path-aware parse diagnostics rendered at
  three boundaries, HTML form metadata and redisplay, compiled trusted-lane JSON codecs for storage writes
  ([JSON Codec]({{< relref "/schema/json-codec.md" >}})), and production-only
  [contextual rules]({{< relref "/schema/rules.md" >}}).
- **Flow where orchestration warrants it.** Persistence and id generation are environment services; use cases
  are readable typed workflows. Flow is never part of the schema entry price.

`examples/Axial.ReferenceApp/README.md` records the friction this exercise exposed and the API changes it drove —
it is the honest companion to this page.

### Schema-derived property tests

`tests/Axial.ReferenceApp.Tests/SchemaGenTests.fs` uses the non-packable `Axial.Schema.Testing` FsCheck adapter:
`SchemaGen.raw` derives a generator of valid raw boundary inputs from the workspace schema, and `SchemaGen.model`
derives trusted model values. The properties pin three claims with no hand-written fixtures:

- every generated raw input parses through the schema;
- every generated model survives a codec round-trip byte-for-byte;
- every schema-valid wire value maps into the domain (`toDomain` is total over what the schema admits).

The generator honours the schema's own constraints — lengths, ranges, `oneOf`, required — because it reads the
same declaration the parser executes.

## The generated-wire slice

```bash
dotnet run --project examples/Axial.ReferenceApp.Wire/Axial.ReferenceApp.Wire.fsproj --nologo
```

The wire slice answers the question the hand-written tier leaves open: what does the day-to-day authoring
experience look like once wire schemas are generated? You own an ordinary record with constraint attributes:

```fsharp
[<WireSchema>]
type WorkspaceCard =
    { [<Min 1; Max 60>] Name: string
      [<Email; WireName "owner_email">] OwnerEmail: string
      [<Default "private">] Visibility: Visibility
      [<Distinct>] Members: string list }
```

`schemagen` writes the sibling `workspace.g.fs`: the schema pipeline you would have written by hand, `parse` and
`validate`, typed `Fields` references, and — because `WorkspaceCardV1`/`WorkspaceCard` follow the version-chain
naming convention — a `WorkspaceCard.contract` builder that takes your typed v1 → v2 migration. The hand-written
surface shrinks to exactly the parts that carry meaning: the migration, the strict domain mapping (`TrustedCard`
rejects an owner listed as a member — a rule the wire deliberately cannot express), and a head-version write
through a compiled codec. Generated schemas are ordinary schemas, so `JsonSchema.generate` and `Json.compile`
come along for free.

See [Versioned Contracts]({{< relref "/schema/contracts.md" >}}) for the full attribute vocabulary, the
`.contract` grammar alternative, and running generation in your build with `Axial.Schema.Contracts.Build`.

## One boundary contract, two HTTP hosts

`examples/Axial.Api` (ASP.NET Core) and `examples/Axial.Api.GenHttp` (GenHTTP) serve the same schema-driven
boundary: the request body parses through the schema (invalid input becomes an RFC 9457 problem-details 400 with
JSON-pointer paths), the trusted model flows through an ordinary application Flow, the response serializes
through the compiled codec, and `/openapi.json` is assembled from the same declaration.

```bash
AXIAL_EXAMPLE=smoke dotnet run --project examples/Axial.Api/Axial.Api.fsproj --nologo
AXIAL_EXAMPLE=smoke dotnet run --project examples/Axial.Api.GenHttp/Axial.Api.GenHttp.fsproj --nologo
```

The point of the twin is that nothing schema-facing changes between hosts: `Axial.Schema.Http` owns raw input,
problem details, and OpenAPI assembly; each host package adapts its own request/response idiom. Routing and app
wiring remain the host's ([HTTP Servers]({{< relref "/schema/http-servers.md" >}})).
