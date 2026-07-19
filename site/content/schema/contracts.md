---
weight: 65
title: Versioned Contracts
description: Permissive wire schemas mapped to strict domain types, explicit versioning when the wire evolves, and schema generation from your own [<DeriveSchema>] records.
type: docs
---


This page shows how to handle wire payloads that are shaped by their format and outlive the code that wrote them.
Stored configuration, queued messages, and saved events keep the shape they had when they were written; your
domain model is neither shaped by a wire format nor frozen in time. The pattern is: a permissive **wire schema**
per format, a strict **domain schema**, an ordinary mapping function between them, and the `Contract<'model>`
engine when the wire needs versioning. When wire schemas multiply, you stop hand-writing them: mark your DTO
record with `[<DeriveSchema>]` and `schemagen` derives the schema from it.

Generated contract types remain wire types. Keep them public and easy to serialize. Map them into private or refined
domain types before business code relies on their values.

## Wire Schemas and Domain Schemas

For a wire format you generally want a DTO shaped per format — field names, nesting, and types that match what is
actually on the wire. For your program you want a real domain type — invariants, smart constructors, honest unions.
Both are schemas; they differ in how much they accept:

- The **wire schema** is open. It admits anything the format can legitimately carry, with light constraints
  (formats, lengths) — its job is to establish shape, not enforce business rules. Its result is a plain public
  record.
- The **domain schema** is strict. It lives on a hand-written F# type whose constructor enforces the invariants
  (see [Construction Guarantees]({{< relref "/schema/trusted-construction.md" >}})).

The step between them is an ordinary function — and this is where strictness lives:

```fsharp
// Wire DTO: shaped like the JSON, permissive.
type OrderWire = { Sku: string; Quantity: int }

// Domain: strict, hand-written, invariant-bearing.
// Order.create : string -> int -> Result<Order, OrderError>

let toDomain (wire: OrderWire) : Result<Order, OrderError> =
    Order.create wire.Sku wire.Quantity
```

Parsing a boundary payload is then `Schema.parse` against the wire schema followed by `toDomain`. Keeping the
tiers separate is what makes the rest of this page cheap: the wire side can change shape, gain versions, and be
generated, without the domain type ever knowing.

## Versioning the Wire

When old payloads must keep parsing after the wire shape changes — events, messages, config files — versioning
enters, and it enters on the wire tier. A contract is a chain of frozen wire schemas plus explicit migrations:

- Every version keeps its own schema. `Config.v1` payloads parse against the v1 schema exactly as they always did.
- Migrations are hand-written, typed, and contiguous: each one takes the version n-1 model to the version n model
  and may fail with a reason. There is no automatic structural migration — renaming and defaulting decisions are
  code you can read and test.
- Parsing is detect version → parse against that frozen schema → migrate forward step by step → re-check against
  the current schema. A successful `Contract.parse` has passed the current schema's gates, so the result is the
  same value the current wire schema's `Schema.parse` produces — ready for the same `toDomain` map, which stays a
  single function no matter how many wire versions accumulate behind it.

## Declaring a Contract by Hand

`Contract.create` starts at the current version; `Contract.supersedes` registers each immediately preceding
version with its migration; `Contract.build` fixes how the version is detected.

```fsharp
open Axial.Schema

// Frozen v1 wire shape and its schema (unchanged since it shipped).
type ConfigV1 = { Host: string }

// Current shape: v2 split the host into host + port.
type Config = { Host: string; Port: int }

let migrateV1ToV2 (v1: ConfigV1) : Result<Config, MigrationError> =
    match v1.Host.Split ':' with
    | [| host; port |] ->
        match System.Int32.TryParse port with
        | true, parsed -> Ok { Host = host; Port = parsed }
        | false, _ -> Error(MigrationError.MigrationFailed $"unreadable port in '{v1.Host}'")
    | _ -> Ok { Host = v1.Host; Port = 5432 }

let configContract : Contract<Config> =
    Contract.create "Config" 2 configSchema
    |> Contract.supersedes 1 configV1Schema migrateV1ToV2
    |> Contract.build (VersionSource.Field "schemaVersion")
```

`VersionSource` names where the version comes from:

- `VersionSource.Field "schemaVersion"` reads a wire field from the input.
- `VersionSource.External` means the caller knows the version out of band (a column, a header, a topic name) and
  calls `Contract.parseVersion contract version raw`.
- `VersionSource.UnversionedMeans 1` treats input with no version marker as a specific registered version — the
  usual story for data that predates versioning.

Parsing returns the current model or a `ContractError` that says exactly what went wrong:

```fsharp
match Contract.parse configContract raw with
| Ok config -> printfn $"{config.Host}:{config.Port}"
| Error ContractError.VersionMissing -> eprintfn "no readable schemaVersion field"
| Error (ContractError.VersionUnrecognized version) -> eprintfn $"version {version} was never registered"
| Error (ContractError.VersionTooNew(found, supported)) -> eprintfn $"payload is v{found}, this build supports up to v{supported}"
| Error (ContractError.ParseFailed(version, diagnostics)) -> eprintfn $"v{version} payload is malformed: %A{diagnostics}"
| Error (ContractError.Migration failure) -> eprintfn $"migration failed: %A{failure}"
```

`ContractError.ParseFailed` and `MigrationError.RevalidationFailed` carry the same path-aware
`Diagnostics<SchemaError>` as `Schema.parse`, so one renderer displays every boundary failure.

## Start from a Record

Everything above is hand-written and stays reasonable at small scale. But every wire schema is a record plus a
builder pipeline that must never drift apart, and when wire schemas multiply, maintaining the pipelines is purely
mechanical work. That mechanical part is generated — from the record you would have written anyway:

```fsharp
// wire/orders.fs — ordinary user-owned F#
namespace MyApp.Wire

open Axial.Schema.Derive

[<DeriveSchema>]
type OrderWire =
    { /// Stock keeping unit.
      [<Pattern @"^[A-Z]{3}-\d+$">]
      Sku: string
      [<AtLeast 1>]
      Quantity: int
      Note: string option }
```

Running `schemagen` over the file writes a checked-in sibling `orders.g.fs` containing the module you would have
written by hand: `OrderWire.schema`, `OrderWire.parse`, `OrderWire.validate`, and typed `OrderWire.Fields`
references. You own the record; the schema is derived. A bare `[<DeriveSchema>]` record with no other attributes is
the everyday case — a shape-only permissive schema, exactly the ceremony of a `System.Text.Json` DTO.

The details:

- **Constraints are opt-in attributes** mirroring the schema constraint vocabulary: `Pattern`, `Min`/`Max` (text
  length, list/map count), `AtLeast`/`GreaterThan`/`AtMost`/`LessThan`/`MultipleOf`, `Distinct`, `Email`, and
  `Default` (schema metadata, surfaced in generated JSON Schema). `[<SchemaName "customer_note">]` overrides one
  wire name; otherwise fields are camelCased (`--naming snake` switches the policy per run).
- **The compiler is the drift detector.** The generated module constructs your record by name — rename, add, or
  remove a field without regenerating and the stale `.g.fs` fails to compile, pointing at the exact field.
- **The type vocabulary is the wire vocabulary**: `string`, `int`, `decimal`, `bool`, `DateOnly`,
  `DateTimeOffset`, `Guid`, `option`, `list`, `Map<string, _>`, references to other `[<DeriveSchema>]` records in
  the same file, nullary unions (an enum: case names become wire tags), and `[<DeriveUnion "kind">]` unions whose
  cases each carry one marked record payload (an internally tagged union). Anything else — `float`, tuples,
  arrays, generics — is a generation-time error with guidance.
- **Version chains use the same naming convention the generator emits**: `ProfileV1`, `ProfileV2` are frozen
  superseded versions, the bare `Profile` is current, and the current module gains a `contract` builder taking
  your migrations as typed parameters. `[<DeriveSchema(Chain = "Profile", Version = 1)>]` overrides the convention
  when a name doesn't fit it.

  ```fsharp
  [<DeriveSchema>]
  type ProfileV1 = { Name: string }          // frozen v1 wire shape

  [<DeriveSchema>]
  type Profile = { Name: string; Email: string }  // current (v2)

  // Generated: Profile.contract takes the v1 -> v2 migration and the VersionSource.
  let profileContract =
      Profile.contract
          (fun v1 -> Ok { Name = v1.Name; Email = "" })
          (VersionSource.Field "schemaVersion")
  ```
- **A custom constructor replaces the record literal** when you want to normalise values on the way in.
  `[<SchemaConstructor "OrderWire.create">]` on the record makes the generated schema call that function —
  fields in declaration order, returning the record type — instead of assembling a record literal. Declare it
  as a static member on the record (`static member create sku quantity = ...`): the generated module takes the
  record's name, so a user module of the same name would not compile. The name is emitted verbatim into the
  generated code.
- **Doc comments carry through** to the schema, generated JSON Schema, and XML docs.
- **Nothing runs at runtime.** Attributes are inert metadata read from source text at generation time — no
  reflection, and the generated output is ordinary schema code, so Fable and NativeAOT/trimming support are
  unchanged.

## The `.contract` Alternative

The same generator also accepts `.contract` files — a small declaration grammar for when you'd rather the
declaration own the record too (the generator emits both the record and its schema). It produces exactly the same
generated shape from the same constraint vocabulary. The grammar has no expressions — literals and names only —
so a declaration can never hide logic (your domain types, constructors, and migrations remain the only places
logic lives):

```text
/// Site polling configuration.
contract Config.v1 {
  /// Stable device identifier issued by the registry.
  deviceId as "device_id": text [ pattern "^[A-Z]{3}-\d+$" ]
  pollSeconds: int [ >= 1, multipleOf 5 ] = 30
  mode: "auto" | "manual" | "off"
  tanks: list Tank.v1 [ min 1, distinct ]
  location?: Geo.v1
  thresholds: map decimal
  shape: union kind {
    circle: Circle.v1
    rect: Rect.v1
  }
}
```

Reading a field line left to right: name, optional `as "wire_name"` rename, `?` for optional, type, `[ constraints ]`,
`= default`.

- **Types**: `text`, `int`, `decimal`, `bool`, `date`, `dateTime`, `guid`, `email`; `list T` and `map T` (map keys
  are always text, as JSON object keys are); a set of string literals (`"auto" | "manual" | "off"`) becomes a
  generated discriminated union; `Tank.v1` references another contract at a pinned version; `union kind { ... }`
  is an internally tagged union whose cases each reference a contract.
- **Constraints**: comparisons (`>= 1`, `< 100`) bound a numeric value; `min`/`max` bound the natural size of the
  type (text length, list/map count); `pattern`, `multipleOf`, and `distinct` match their JSON Schema meanings.
- **Required unless `?`**. There is no `required` keyword and no separate null notion — one absence axis.
- **`///` doc comments** become XML docs on the generated types and descriptions in generated JSON Schema.
  `@deprecated "message"` and friends attach metadata.

Generation buys more than the record and schema. Each contract emits `validate` (check an assembled draft),
`parse` (raw boundary input), and a typed `Fields` module of field references for rules, redisplay, and UI
binding — and because the output is an ordinary `Schema`, everything schemas already do comes along: JSON Schema
output via `JsonSchema.generate` (reject broken payloads before they enter storage), compiled codecs via
`Json.compile`, inspection metadata, and doc comments carried through to XML docs and generated JSON Schema.

## Generation Runs in Your Build

Reference the `Axial.Schema.Contracts.Build` package and declare which sources carry wire declarations; the
generator then runs before compile and keeps the checked-in `.g.fs` siblings fresh — nobody runs a tool by hand,
and unchanged files cost nothing on rebuild (the target is timestamp-incremental):

```xml
<ItemGroup>
  <PackageReference Include="Axial.Schema.Contracts.Build" Version="..." PrivateAssets="all" />
</ItemGroup>

<ItemGroup>
  <AxialDeriveSchema Include="wire/orders.fs" />
  <Compile Include="wire/orders.fs" />
  <Compile Include="wire/orders.g.fs" />
</ItemGroup>
```

`AxialSchemaNaming` (camel | snake | verbatim) sets the naming policy, `AxialSchemaGenEnabled=false` skips
generation, and `.contract` files ride along as `<AxialContract>` items with `AxialContractNamespace`.

The same generator is also a plain CLI for repository development and one-off generation:

```bash
dotnet run --project scripts/schemagen -- src/MyApp/wire
dotnet run --project scripts/schemagen -- --namespace MyApp.Wire src/MyApp/contracts
```

Record- and `.contract`-declared wire types resolve as one set, so either kind may reference the other's
declarations.

## Multiple Versions in One File

Declaring several versions of the same contract — oldest first, no gaps — generates the whole chain:

```text
/// A user profile as first stored.
contract Profile.v1 {
  name: text [ min 1, max 100 ]
  email: email
}

/// A user profile with an explicit marketing consent decision.
contract Profile.v2 {
  name: text [ min 1, max 100 ]
  email: email
  marketingOptIn as "marketing_opt_in": bool = false
}
```

The latest version keeps the bare name (`Profile`); superseded versions get suffixed types and modules
(`ProfileV1`) whose schemas stay frozen. The latest module also gains a `contract` builder that takes each
n-1 → n migration as a typed parameter, so the migrations remain hand-written F# and the compiler enforces that
every step exists and lines up:

```fsharp
// Generated:
//   Profile.contract : (ProfileV1 -> Result<Profile, MigrationError>) -> VersionSource -> Contract<Profile>

let profileContract =
    Profile.contract
        (fun v1 -> Ok { Name = v1.Name; Email = v1.Email; MarketingOptIn = false })
        (VersionSource.Field "schemaVersion")

// A stored v1 payload parses and migrates into the current model.
Contract.parse profileContract storedPayload
```

Cutting a new version is: add the `contract Profile.v3 { ... }` block, regenerate, and follow the compiler — the
builder's signature grows a `Profile (was v2) -> Result<ProfileV3-now-Profile, MigrationError>` parameter and every
call site that constructs the contract fails to compile until the new migration is written.

## What Contracts Deliberately Do Not Do

- **No domain-tier generation.** Generated records are wire shapes. Domain models with real invariants are
  hand-written F# behind their own constructors, and the wire→domain mapping function is ordinary code you own —
  contracts never replace your types, they feed them.
- **No automatic structural migrations.** A tool that guesses field mappings silently deletes data; migrations
  here are code.
- **No schema algebra.** `allOf` and untagged `anyOf` are absent from the grammar on purpose; unions carry a
  discriminator.
- **No second authoring surface.** The grammar emits the one existing `Schema` builder API; nothing generated is
  something you could not have written yourself.

See [Separate Wire And Domain Models](patterns/wire-and-domain-models/) for a complete build-to-domain pattern.
