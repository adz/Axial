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

This is the form to prefer at use sites. The rest of this page expands what `Email` contributes and shows where
schema-local constraints fit.

## Define the domain type

```fsharp
open Axial.Check
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
    constraints [ required; maxLength 80 ]
}
```

Schema constraints remain available directly inside a field block. Here both constraints apply to the incoming
`string`, so interpreters can retain their metadata for diagnostics, forms, and generated schemas.

Constraints preserve the value type, however. This block still contains a `Schema<string>`, while `_.Email` returns
`Email`; by itself, the declaration cannot complete the field.

## Refine after constraining the raw value

Add `refine` after the raw-text constraints to perform that type transition:

```fsharp
let contactSchema =
    schema<Contact> {
        field "email" _.Email {
            withSchema Schema.text
            constraints [ required; maxLength 80 ]
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
    constraints [ required; maxLength 80 ]
    refine Email.refinement
}
```

That limit belongs in this field block rather than in every `Email`. `Email.refinement` still enforces the intrinsic
email-format invariant for every construction path.

Use this inline form for boundary-specific restrictions. If a constraint must hold for every instance of the domain
type, lift it into the refinement instead.

## Lift universal constraints into the refinement

If required presence and the length limit define every `ContactEmail`, put them beside the domain type instead:

```fsharp
type ContactEmail = private ContactEmail of string

module ContactEmail =
    let value (ContactEmail value) = value

    let refinement =
        Refinement.defineAll
            [ Axial.Check.Constraint.required
              Axial.Check.Constraint.email
              Axial.Check.Constraint.maxLength 254 ]
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

See [Define Refined Types]({{< relref "/error-handling/refined/domain-values/" >}}) for domain-side definitions.
