---
weight: 25
title: Domain Values
description: Author caller-owned refined domain values for standalone use and schema fields.
type: docs
---


This page shows how to author caller-owned domain value types that work both as standalone smart constructors and as
schema fields.

Use this shape when a built-in catalog type such as `NonBlankString`, `Slug`, or `PositiveInt` is too generic for your
model. Keep the invariant in one private type module, then expose whichever entry points the boundary needs.

## One Type Module

Start with a private constructor, a value extractor, and a smart constructor:

```fsharp
open Axial.ErrorHandling
open Axial.Refined

type ContactEmail = private ContactEmail of string

module ContactEmail =
    let value (ContactEmail value) = value

    let create value : Result<ContactEmail, RefinementError> =
        Refine.withCheck
            "ContactEmail"
            (Check.all [
                Check.String.present
                Check.String.email
                Check.String.maxLength 254
            ])
            ContactEmail
            value
```

The constructor stays private, so the rest of the application cannot accidentally build an invalid `ContactEmail`.
`create` is the standalone boundary helper for code that already has a parsed string.

## Schema Field

Add a `Schema.refine` schema in the same module when the value appears inside a larger input model:

```fsharp
open Axial.Schema

module ContactEmail =
    let value (ContactEmail value) = value

    let create value : Result<ContactEmail, RefinementError> =
        Refine.withCheck
            "ContactEmail"
            (Check.all [
                Check.String.present
                Check.String.email
                Check.String.maxLength 254
            ])
            ContactEmail
            value

    let schema : Schema<ContactEmail> =
        Schema.text
        |> Schema.constrainAll [
            Constraint.required
            Constraint.email
            Constraint.maxLength 254
        ]
        |> Schema.refine create SchemaError.ofRefinementError value
        |> Schema.withFormat SchemaFormat.email
```

`Schema.refine` takes the real fallible smart constructor and both directions. `create` remains authoritative: its
failures lower into path-aware schema diagnostics through `SchemaError.ofRefinementError`. The extractor lets
checking, codecs, documentation, and UI interpreters recover the raw representation from an existing trusted value.
The raw constraints supply portable metadata for JSON Schema, forms, and generators; if they drift from `create`,
refinement still returns diagnostics rather than admitting the value.

Keep raw parsing and path-aware diagnostics in the schema layer. Keep value-only facts in `Check` and the smart
constructor. Use `constructResult` for whole-record invariants that need multiple fields.

## Optional Parse Helper

Add parse helpers only when callers commonly start from serialized text outside a schema boundary:

```fsharp
module ContactEmail =
    let parse text : Result<ContactEmail, RefinementError> =
        create text
```

Do not add a helper only to mirror every schema field. The schema is the preferred entry point for whole input models.

## Use The Value

```fsharp
type Signup =
    {
        Email: ContactEmail
    }

let signupSchema =
    Schema.define<Signup>
    |> fieldWith ContactEmail.schema "email" _.Email
    |> construct (fun email -> { Email = email })
```

For built-in catalog types, prefer `Axial.Schema.RefinedSchemas` instead of re-authoring local wrappers.
