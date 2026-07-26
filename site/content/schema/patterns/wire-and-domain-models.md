---
title: Separate Wire And Domain Models
weight: 30
description: Generate permissive wire schemas during the build and map them into hand-written domain types.
type: docs
---


Wire formats and domain models change for different reasons. A stored payload may need old field names and versions.
Business code needs types that make current invariants easy to rely on.

A **wire model** represents serialized data such as JSON, a message, or a stored document. A **domain model** represents
the business concept used by application logic. An **invariant** is a rule every valid domain value must satisfy.

Keep those jobs in separate types.

## Define a permissive wire record

`[<DeriveSchema>]` marks a public, namespace-level record whose Axial schema is generated from its source declaration. The
schema describes how input fields parse and which portable constraints apply.

```fsharp
namespace MyApp.Contracts

open Axial.Schema.Derive

[<DeriveSchema>]
type BookingWire =
    { Start: DateOnly
      End: DateOnly
      [<SchemaName "customer_note">]
      Note: string option }
```

Wire records remain public on purpose. They describe data at a boundary; they do not claim that business invariants
hold.

## Run generation as part of the build

Reference the build package. `[<DeriveSchema>]` records are discovered in the project's ordinary F# compile files;
generated code is placed under `obj` and inserted in the correct compile order automatically.

```xml
<ItemGroup>
  <PackageReference
      Include="Axial.Schema.Contracts.Build"
      Version="..."
      PrivateAssets="all" />
</ItemGroup>

```

Set `AxialSchemaGeneratedFiles` to `CheckedIn` when the repository deliberately commits generated siblings. Do not add
those `.g.fs` files to `<Compile>`; the target inserts them after their declaration files.

The generated module contains `schema`, `parse`, and `validate`. Stale generated code is replaced during the normal
build; no separate generator command is needed.

## Admit the wire value into the domain

Map the public wire record into a private aggregate through its real constructor.

```fsharp
let toDomain (wire: BookingWire) =
    Booking.create
        { Start = wire.Start
          End = wire.End }
```

The complete boundary is explicit:

```fsharp
let parseBooking raw =
    (BookingWire.parse raw).Result
    |> Result.bind toDomain
```

After this function succeeds, pass `Booking` into application code. Do not pass `BookingWire` inward and rely on every
caller to remember which checks ran.

## Map back explicitly

Persistence and response code use a named domain-to-wire function.

```fsharp
let fromDomain booking : BookingWire =
    { Start = Booking.start booking
      End = Booking.finish booking
      Note = None }
```

This mapping is the review point for representation choices such as omitted fields, defaults, and renamed properties.

## Add versions on the wire side

Keep old wire records and typed migrations in the Contracts project. Migrate to the current wire type, then perform the
single current wire-to-domain mapping.

```text
stored input
  -> parse frozen wire version
  -> typed migrations
  -> current wire value
  -> Booking.create
  -> domain Booking
```

The generator emits the typed version-chain builder. Adding a version changes that builder, so contract construction
does not compile until the new migration is supplied.

See [Versioned Contracts](../contracts/) for the attribute vocabulary, `.contract` grammar, and migration setup.
