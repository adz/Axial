---
weight: 40
title: Refined Schemas
type: docs
description: Reuse one bidirectional refinement in direct construction, schema parsing, checking, and encoding.
---

# Refined Schemas

`Axial.Refined` owns the domain construction definition. Schema applies that definition at a structured boundary.

```fsharp
type Email =
    private
    | Email of string

module Email =
    let create raw =
        if System.Net.Mail.MailAddress.TryCreate raw |> fst then
            Ok(Email raw)
        else
            Error(RefinementError.custom "email" "Expected an email address.")

    let value (Email raw) = raw
    let refinement = Refinement.define create value

type Email with
    static member Refinement(_: string, _: Email) =
        Email.refinement
```

The descriptor contains both directions. Parsing calls its fallible constructor; checking and encoding inspect an
existing `Email` back to `string`.

## A standalone value schema

```fsharp
let emailSchema : Schema<Email> =
    Schema.text
    |> Schema.constrainAll [ Constraint.required; Constraint.email ]
    |> Schema.refine Email.refinement
    |> Schema.withFormat SchemaFormat.email
```

`Schema.refine` receives one named value instead of separate construction, error mapping, and inspection functions.

## A type-directed field

```fsharp
let contactSchema =
    schema<Contact> {
        field "email" _.Email {
            withSchema Schema.text
            constrain Constraint.required
            constrain Constraint.email
            refine
        }

        construct Contact.create
    }
```

Before `refine`, the current field type is `string`. The getter returns `Email`, so the operation resolves
`Refinement<string,Email>`. The block must finish at `Email`.

If `Email` contributes a canonical schema, the field is shorter:

```fsharp
type Email with
    static member Schema(_: Email) =
        emailSchema

schema<Contact> {
    field "email" _.Email
    construct Contact.create
}
```

Options, lists, and string-keyed maps resolve the canonical item schema recursively.

## Portable constraints and executable validation

```fsharp
field "email" _.Email {
    withSchema Schema.text
    constrain Constraint.required       // portable raw text rule
    refine                              // string -> Email
    validate validateCompanyEmail       // executable Email rule
}
```

Constraints can become JSON Schema, HTML attributes, documentation, or generators. An arbitrary `validate` function
runs during parsing and checking but cannot be translated automatically.

The smart constructor must still enforce the intrinsic domain invariant. Direct `Refine.from` calls do not pass
through Schema constraints.

## Built-in schemas

Built-in refined types have canonical schemas. `RefinedSchemas` also exposes configured forms:

```fsharp
RefinedSchemas.boundedString 2 80
RefinedSchemas.boundedList 1 10 Schema.guid
RefinedSchemas.dateTimeOffsetRange
```

See [Define Refined Types]({{< relref "/error-handling/refined/domain-values/" >}}) for the complete domain-side
definition.
