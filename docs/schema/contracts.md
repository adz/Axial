---
weight: 65
title: Versioned Contracts
description: Parse every stored wire version into one current model with explicit migrations, and generate versioned schemas from .contract files at scale.
---

# Versioned Contracts

This page shows how to read wire payloads that outlive the code that wrote them. Stored configuration, queued
messages, and saved drafts keep the shape they had when they were written; the code reading them has moved on.
`Contract<'model>` parses any registered version and migrates it forward into the one current model, and the
`.contract` grammar with the `schemagen` generator keeps that tolerable when you have many versioned boundaries.

Contracts are the at-scale tier, not an entry point. A team with a handful of models and no version churn never
needs them: `Schema.parse` against the current schema is the whole story. Reach for `Contract` when old payloads
must keep parsing after the shape changes, and for `.contract` generation when the number of versioned schemas
makes hand-maintaining them a chore. The generator emits the same `Schema` builder code you would write by hand —
reading the generated file is understanding the system. Contracts are the **wire tier only**: domain models stay
hand-written F# (see [Construction Guarantees]({{< relref "/schema/trusted-construction.md" >}})).

## The Versioning Model

A contract is a chain of frozen wire schemas plus explicit migrations:

- Every version keeps its own schema. `Config.v1` payloads parse against the v1 schema exactly as they always did.
- Migrations are hand-written, typed, and contiguous: each one takes the version n-1 model to the version n model
  and may fail with a reason. There is no automatic structural migration — renaming and defaulting decisions are
  code you can read and test.
- Parsing is detect version → parse against that frozen schema → migrate forward step by step → re-check against
  the current schema. A successful `Contract.parse` has passed the current schema's field and constructor gates,
  so the result is the same trusted `'model` that `Schema.parse` produces.

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

## The `.contract` Grammar

When versioned schemas number in the dozens, hand-maintaining the frozen wire records and builder pipelines is
mechanical work. A `.contract` file declares the wire shape as pure data and `schemagen` emits ordinary checked-in
F#. The grammar has no expressions — literals and names only — so a declaration can never hide logic:

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

Each contract generates a public wire record, a `schema`, `validate` (check an assembled draft), `parse` (raw
boundary input), and a typed `Fields` module of field references for rules, redisplay, and UI binding.

## Generating with `schemagen`

`schemagen` reads `.contract` files and writes a sibling `.g.fs` for each, which you check in and compile like any
other source:

```bash
dotnet run --project scripts/schemagen -- --namespace MyApp.Wire src/MyApp/contracts
```

In CI, `--check` writes nothing and exits nonzero if any generated file is missing or stale, so the checked-in F#
can never drift from its declaration:

```bash
dotnet run --project scripts/schemagen -- --namespace MyApp.Wire --check src/MyApp/contracts
```

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
  hand-written F# behind their own constructors.
- **No automatic structural migrations.** A tool that guesses field mappings silently deletes data; migrations
  here are code.
- **No schema algebra.** `allOf` and untagged `anyOf` are absent from the grammar on purpose; unions carry a
  discriminator.
- **No second authoring surface.** The grammar emits the one existing `Schema` builder API; nothing generated is
  something you could not have written yourself.
