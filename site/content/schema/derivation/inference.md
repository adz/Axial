---
title: Schema Inference
linkTitle: Inference
description: How schemagen turns F# records, fields, unions, comments, and constructors into Schema DSL.
weight: 30
type: docs
---


`schemagen` parses F# source and lowers marked declarations to typed Schema DSL. It does not load your assembly or
inspect types at runtime.

## Record shape

A derivable record must be:

- public and declared at namespace level;
- non-generic;
- in a file with one namespace for generated declarations; and
- marked with `[<DeriveSchema>]`.

Every record field becomes one schema field in declaration order. By default the generated `construct` expression
creates a record literal. A single `[<SchemaConstructor>]` static member replaces that literal; it must accept fields in
declaration order and return the record type.

The generated module uses the record's type name and exposes `schema`, `parse`, and `validate`.
Generated code refers to your fields by name. Adding, removing, renaming, or changing a field without regenerating makes
F# compilation fail rather than allowing the schema to drift.

## Field type inference

| F# field type | Inferred schema |
| --- | --- |
| `string` | `Schema.text` |
| `int` | `Schema.int` |
| `decimal` | `Schema.decimal` |
| `bool` | `Schema.bool` |
| `DateOnly` | `Schema.date` |
| `DateTimeOffset` | `Schema.dateTime` |
| `Guid` | `Schema.guid` |
| `'a option` | optional field using the inferred `'a` schema |
| `'a list` | `Schema.listWith` the inferred element schema |
| `Map<string, 'a>` | `Schema.mapWith` the inferred value schema |
| another marked record in the same file | that record's generated schema |
| a nullary discriminated union | enum schema; case names become tags |
| a `[<DeriveUnion "field">]` union | internally tagged union of marked record payloads |

The wire vocabulary is intentionally closed. Arrays, tuples, generic records, nested options, floating-point types,
non-string map keys, other integer widths, and unknown application types produce generation diagnostics. Use `list`,
`decimal`, `int`, or an explicitly mapped domain boundary instead of silently changing wire semantics.

Marked-record references stay within one source file so generation and F# compile ordering remain deterministic.

## Names

The default naming policy is `camel`, so `MarketingOptIn` becomes `marketingOptIn`. Set
[`AxialSchemaNaming`](../msbuild/#msbuild-properties) to `snake` or `verbatim` for the whole project. Use
`[<SchemaName "marketing_opt_in">]` on one field or nullary union case to override the policy locally.

## Options, supplied fields, and defaults

An `option` field represents wire absence and parses an omitted key as `None`. There is no second nested-option absence
axis. `[<Supplied>]` is different: it requires the key to occur in the input even when the typed value could otherwise
be produced. `[<Default value>]` supplies an omitted non-optional field and cannot be used on an option field.

## Constraints and metadata

Field attributes are lowered in source order to the operations documented in the
[attribute table](../attributes/#field-attributes). Constraints remain executable and inspectable, so parsing,
`Schema.check`, JSON Schema, forms, and diagnostics all observe the same rule. `Format` is metadata only. XML `///`
comments become `Schema.describe` metadata and generated XML documentation.

## Union inference

A union whose cases have no payload can be used as a field type without marking the union:

```fsharp
type Plan = Free | Team | Enterprise

[<DeriveSchema>]
type Signup = { Plan: Plan }
```

Tags follow the naming policy and can be overridden with `[<SchemaName>]` on a case.

Payload unions must opt into an internal discriminator. Every case must carry exactly one marked record from the same
file:

```fsharp
[<DeriveSchema>]
type Card = { LastFour: string }

[<DeriveSchema>]
type Bank = { Iban: string }

[<DeriveUnion "kind">]
type Payment =
    | Card of Card
    | Bank of Bank

[<DeriveSchema>]
type Checkout = { Payment: Payment }
```

The generated schema follows the handwritten [union schema rules](../union-schemas/).

## Version-series inference

Names ending in `Vn` are grouped by convention. For example, `ProfileV1`, `ProfileV2`, and a bare `Profile` form a
contiguous series, with the bare record inferred as the current version. Use
`[<DeriveSchema(Chain = "Profile", Version = 1)>]` when names do not follow the convention. A series additionally gets
a typed `contract` builder whose parameters are the explicit migrations between adjacent versions; see
[Versioned Contracts](../contracts/).
