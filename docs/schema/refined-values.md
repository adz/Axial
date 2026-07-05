---
weight: 20
title: Refined Value Schemas
type: docs
description: Domain values like Email as portable field schemas.
---

# Refined Value Schemas

A refined value schema pairs a raw representation with a domain type, so fields can carry real domain values — `Email`,
`Quantity`, `ContactName` — while boundary interpreters keep working with the raw representation.

## Authoring

`Value.refined` takes a construction function, an inspection function, and the raw value schema:

```fsharp
type Email = private Email of string

module Email =
    let create (value: string) = Email value
    let value (Email value) = value

    let schema : ValueSchema<Email> =
        Value.text
        |> Value.withConstraint SchemaConstraint.required
        |> Value.refined create value
        |> Value.withConstraint SchemaConstraint.email
        |> Value.withFormat SchemaFormat.email
```

Both directions are required by design. Construction lets input parsing produce the refined value from checked raw
text; inspection lets validation, codecs, documentation, and UI interpreters recover the raw representation from an
existing trusted model.

`create` is intentionally total: interpreters run the raw schema's constraints first, so construction only happens over
text that already passed `required` and `email`. Keep failure-capable invariants in constructor results
(`Schema.buildResult`) or dedicated smart constructors, not inside `Value.refined`.

## Using Refined Schemas As Fields

Refined value schemas are ordinary `ValueSchema<'value>` values, used with the generic `Schema.field`:

```fsharp
let contactSchema =
    Schema.recordFor<Contact, _> (fun email name -> { Email = email; Name = name })
    |> Schema.field "email" _.Email Email.schema
    |> Schema.field "name" _.Name ContactName.schema
    |> Schema.build
```

Primitive fields keep the short path (`Schema.text`, `Schema.int`, …); the generic `Schema.field` is the explicit path
for refined/domain schemas, nested schemas, and collections.

## Layering And Inspection

Constraints can attach to either layer, and both stay inspectable:

- constraints on the raw schema (before `Value.refined`) describe the boundary representation —
  `Value.rawConstraints` returns them
- constraints on the refined schema describe the domain value — `Value.constraints` returns them
- `Value.underlyingPrimitiveKind` sees through any number of refinement layers to the primitive foundation
- `Value.format` reports the nearest declared format through refinement layers

Metadata interpreters use the same visibility through the [Inspect API]({{< relref "/reference/schema/" >}}):
a refined field describes itself as `ValueShape.Refined` wrapping its raw description, so JSON Schema and UI
interpreters can render the boundary representation without knowing the domain type.
