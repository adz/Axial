---
weight: 65
title: Versioned Contracts
description: Keep frozen wire schemas readable through explicit, typed migrations.
type: docs
---


A `Contract<'model>` is a chain of frozen wire schemas and explicit migrations. Use it when stored configuration,
queued messages, or events must remain readable after their wire shape changes. Contract versioning belongs at the
wire boundary; map the current wire value into a strict domain type afterwards.

Schema derivation is documented separately. Start with [Derived Schemas](../derivation/) to generate schemas from
ordinary `[<DeriveSchema>]` F# records, configure MSBuild, and see every supported attribute.

## Wire and domain models

A wire model describes what the format can carry. Keep it public and permissive. A domain model describes what business
code may rely on and should protect its invariants with refined values or private construction.

```fsharp
// Wire DTO: shaped like persisted input.
type OrderWire = { Sku: string; Quantity: int }

// Domain constructor owns business invariants.
// Order.create : string -> int -> Result<Order, OrderError>
let toDomain (wire: OrderWire) =
    Order.create wire.Sku wire.Quantity
```

Parse or migrate to the current wire model first, then call `toDomain`. See
[Separate Wire and Domain Models](./patterns/wire-and-domain-models/).

## Declare a contract by hand

`Contract.create` starts with the current version and schema. Add each immediately preceding version with
`Contract.supersedes`, then choose how the input version is discovered with `Contract.build`.

```fsharp
open Axial.Schema

type ConfigV1 = { Host: string }
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

Migrations are hand-written, typed, contiguous, and may fail. Parsing selects the frozen schema for the input version,
parses it, migrates each adjacent step, and checks the result against the current schema. There is no automatic
structural migration.

## Version sources

| Source | Use |
| --- | --- |
| `VersionSource.Field "schemaVersion"` | Read a positive integer version from an input field. |
| `VersionSource.External` | The caller knows the version out of band and calls `Contract.parseVersion`. |
| `VersionSource.UnversionedMeans 1` | Treat input without a marker as one registered version. |

```fsharp
match Contract.parse configContract raw with
| Ok config -> printfn $"%s{config.Host}:%d{config.Port}"
| Error ContractError.VersionMissing -> eprintfn "no readable schema version"
| Error (ContractError.VersionUnrecognized version) -> eprintfn $"version %d{version} is not registered"
| Error (ContractError.VersionTooNew(found, supported)) ->
    eprintfn $"payload is v%d{found}; this build supports v%d{supported}"
| Error (ContractError.ParseFailed(version, diagnostics)) ->
    eprintfn $"v%d{version} payload is malformed: %A{diagnostics}"
| Error (ContractError.Migration failure) -> eprintfn $"migration failed: %A{failure}"
```

`ContractError.ParseFailed` and `MigrationError.RevalidationFailed` carry the same path-aware `SchemaErrors` used by
`Schema.parse`.

## Generate a version series from records

The generator groups marked records ending in `Vn` and treats a bare record as the current version:

```fsharp
open Axial.Schema.Derive

[<DeriveSchema>]
type ProfileV1 = { Name: string }

[<DeriveSchema>]
type Profile = { Name: string; Email: string }
```

It generates the frozen schemas and a typed builder requiring every adjacent migration:

```fsharp
let profileContract =
    Profile.contract
        (fun v1 -> Ok { Name = v1.Name; Email = "" })
        (VersionSource.Field "schemaVersion")
```

When names do not follow the convention, set `Chain` and `Version` explicitly:

```fsharp
[<DeriveSchema(Chain = "Profile", Version = 1)>]
type LegacyProfile = { Name: string }
```

See [Schema Inference](../derivation/inference/#version-series-inference) for grouping rules and
[Build Generation](../derivation/msbuild/) for setup.

## Design rules

- Keep every shipped wire schema frozen.
- Write and test each adjacent migration; do not infer renames or defaults automatically.
- Keep generated records at the wire tier and map the current value into the domain.
- Revalidation after migration ensures the result passes the current schema.
- Generated output is ordinary Schema DSL code; contract versioning does not introduce runtime reflection.
