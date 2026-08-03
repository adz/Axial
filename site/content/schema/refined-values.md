---
weight: 40
title: Refined Schemas
type: docs
description: Move between canonical refined fields, explicit raw schemas, local constraints, and reusable domain refinements.
---


A field can be short when its type contributes a canonical schema:

```fsharp
let contactSchema =
    schema<Contact> {
        field "email" _.Email
        construct Contact.create
    }
```

This is the form to prefer at use sites. Every built-in refined type from
[Axial.Refined]({{< relref "/values/refined/" >}}) works this way — `NonBlankString`,
`FiniteFloat`, `UnitInterval`, `NonEmptyList<_>`, and the rest resolve without a `withSchema`, as
[Getting Started](../getting-started/) shows.

Rules that need a parameter, such as a length range or a pattern, are constraints rather than types. They belong on
the field, because the bounds are a property of *this* field rather than of the value:

```fsharp
field "name" _.Name {
    constrain Constraint.present
    constrain (Constraint.lengthBetween 2 80)
}
```

Expressing them this way means there is only ever one set of bounds — the schema's. An earlier `BoundedString` type
recorded the bounds it happened to be constructed under, so a value refined at `1..99` was still a `BoundedString`
when checked against a `2..80` schema, and the schema had to re-run its own bounds anyway.

Where a length or format rule *should* be part of a type, define your own refined type for it, as
[Lift universal constraints into the refinement](#lift-universal-constraints-into-the-refinement) shows. The test is
whether any later operation relies on the rule; see
[When not to make a type]({{< relref "/values/refined/catalog/#when-not-to-make-a-type" >}}).

The rest of this page expands what a domain type like `Email` contributes and shows where schema-local constraints fit.

## Define the domain type

```fsharp
open Axial.Constraint
open Axial.Refined

type Email = private Email of string

module Email =
    let value (Email value) = value

    let refinement =
        Refinement.define
            Constraint.email
            Email
            value

    let create value = Refinement.create refinement value
```

The `email` format is intrinsic to `Email`, so its refinement owns that constraint.

## Apply constraints in the Schema DSL

Expand the field to expose its wire schema:

```fsharp
open Axial.Schema
open Axial.Schema.Syntax

field "email" _.Email {
    withSchema Schema.text
    constraints [ present; maxLength 80 ]
}
```

Schema constraints remain available directly inside a field block. Here both constraints apply to the incoming
`string`, so interpreters can retain their metadata for diagnostics, forms, and generated schemas. The named Schema
constraints use the same executable metadata defined by [Constraints]({{< relref "/values/constraint/constraints/" >}}).

Constraints preserve the value type, however. This block still contains a `Schema<string>`, while `_.Email` returns
`Email`; by itself, the declaration cannot complete the field.

## Refine after constraining the raw value

Add `refine` after the raw-text constraints to perform that type transition:

```fsharp
let contactSchema =
    schema<Contact> {
        field "email" _.Email {
            withSchema Schema.text
            constraints [ present; maxLength 80 ]
            refine Email.refinement
        }

        construct Contact.create
    }
```

The operations run in declaration order:

1. `withSchema Schema.text` starts with `Schema<string>`.
2. `constraints` checks facts that belong to the incoming text while preserving `string`.
3. `refine Email.refinement` constructs `Email`, producing `Schema<Email>` to match the getter.

A raw-text constraint must appear before refinement because it cannot be applied to the resulting `Email` value.

## Keep one-off constraints at the schema boundary

Suppose only the billing form imposes an 80-character transport limit:

```fsharp
field "billingEmail" _.BillingEmail {
    withSchema Schema.text
    constraints [ present; maxLength 80 ]
    refine Email.refinement
}
```

That limit belongs in this field block rather than in every `Email`. `Email.refinement` still enforces the intrinsic
email-format invariant for every construction path.

Use this inline form for boundary-specific restrictions. If a constraint must hold for every instance of the domain
type, lift it into the refinement instead.

## Attach an application constraint

There is no adapter step: Schema takes the same `Constraint` value you would check directly.

```fsharp
let even : Constraint<int> =
    Constraint.custom "must be an even quantity" (fun value -> value % 2 = 0)

field "quantity" _.Quantity {
    constrain even
}
```

An arbitrary predicate is opaque, so it runs during parsing and checking but is documented rather than enforced by
generated schemas. Composing built-ins instead — `Constraint.multipleOf 2` here — keeps the rule inspectable, which is
what lets JSON Schema lower it and SchemaGen generate values that satisfy it. See
[Interpreted and opaque]({{< relref "/values/constraint/constraints/" >}}) for the trade.

## Lift universal constraints into the refinement

If required presence and the length limit define every `ContactEmail`, put them beside the domain type instead:

```fsharp
type ContactEmail = private ContactEmail of string

module ContactEmail =
    let value (ContactEmail value) = value

    let refinement =
        Refinement.defineAll
            [ Axial.Constraint.Constraint.present
              Axial.Constraint.Constraint.email
              Axial.Constraint.Constraint.maxLength 254 ]
            ContactEmail
            value

    let create value = Refinement.create refinement value
```

The schema then carries the complete invariant through one value:

```fsharp
let contactEmailSchema : Schema<ContactEmail> =
    Schema.text
    |> Schema.refine ContactEmail.refinement
    |> Schema.withFormat SchemaFormat.email
```

Contribute that canonical schema once:

```fsharp
type ContactEmail with
    static member Schema(_: ContactEmail) = contactEmailSchema
```

Fields return to the compressed form:

```fsharp
schema<Contact> {
    field "email" _.Email
    construct Contact.create
}
```

The constraint-backed refinement now drives direct construction, Schema diagnostics, inspection metadata, and
applicable wire interpreters.

## Canonical refinement inference inside a field

A type may also contribute one canonical refinement for an underlying/destination pair:

```fsharp
type ContactEmail with
    static member Refinement(_: string, _: ContactEmail) = ContactEmail.refinement
```

Then an explicitly selected raw schema can use bare `refine`:

```fsharp
field "email" _.Email {
    withSchema Schema.text
    refine
}
```

Schema knows both `string` and `ContactEmail` at that declaration site. Use `refine ContactEmail.refinement` when a
local variant must be selected explicitly. Parsing and ordinary refinement construction remain named operations.

## Choose the operation by meaning

- `Schema.refine` constructs an invariant-carrying destination and retains refinement metadata.
- `Schema.convert` performs a total projected mapping.
- `Schema.tryConvert` performs a fallible projected mapping returning `SchemaError list`.
- `Schema.admit` constructs a domain model from a structured draft while preserving its fields.
- `validate` adds executable Schema behavior that has no portable metadata.

See [Define Refined Types]({{< relref "/values/refined/domain-values/" >}}) for domain-side definitions.
