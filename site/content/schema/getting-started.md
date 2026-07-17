---
weight: 2
title: Getting Started
type: docs
description: One schema describing boundary input, domain construction, checking, codecs, and metadata.
---


This page shows how one `Schema<'value>` describes boundary input, domain construction, checking, codecs, and metadata.

Application boundaries often repeat the same facts in a DTO parser, validation rules, JSON configuration, form
metadata, and test generators. A schema keeps those facts in one typed value. It is not a marker attached to a model
type, and it does not make every value of that type valid.

## The catalog

Every completed declaration has the same type:

```fsharp
Schema.text                         // Schema<string>
Schema.int                          // Schema<int>
Schema.option Schema.guid           // Schema<Guid option>
Schema.list Schema.text             // Schema<string list>
RefinedSchemas.nonBlankString       // Schema<NonBlankString>
```

Record schemas use those values directly:

```fsharp
open Axial.Schema

type Signup =
    { Email: string
      Age: int }

let signupSchema : Schema<Signup> =
    Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
    |> Schema.field "email" _.Email
        (Schema.text
         |> Schema.constrainAll [ Constraint.required; Constraint.email ])
    |> Schema.field "age" _.Age
        (Schema.int |> Schema.constrain (Constraint.atLeast 13))
    |> Schema.build
```

`Schema.field` has one job: attach a completed field schema. Constraints, formats, descriptions, defaults,
collections, options, and refinements belong to that field schema rather than to special field overloads.

## Parse boundary input

```fsharp
let raw =
    RawInput.Object(
        Map.ofList
            [ "email", RawInput.Scalar "ada@example.com"
              "age", RawInput.Scalar "42" ])

match (Schema.parse signupSchema raw).Result with
| Ok signup -> printfn "%s" signup.Email
| Error diagnostics ->
    diagnostics
    |> Axial.Validation.Diagnostics.flatten
    |> List.iter (SchemaError.renderDiagnostic >> printfn "%s")
```

Parsing performs shape conversion, runs constraints, and invokes the declared record constructor. Errors retain their
paths, and `ParsedInput` retains the original `RawInput` for form redisplay.

The same interpreter works for a value schema:

```fsharp
let parsedName = Schema.parse RefinedSchemas.nonBlankString (RawInput.Scalar "Ada")
```

## One catalog for values and models

A string, a refined name, a list, a union, and a record are all typed descriptions interpreted in the same ways, so
the catalog exposes them through one module:

```fsharp
Schema.text |> Schema.constrain Constraint.email
Schema.parse signupSchema raw
```

There is no separate field-level module to learn. The same combinators and interpreters apply whether `'value` is a
scalar, a collection, or a whole model; whatever distinction the implementation needs stays internal.

`RefinedSchemas` is a sibling namespace only because it is a named catalog of schemas corresponding to
`Axial.Refined` types. Its members produce ordinary `Schema<'value>` values.

## What successful parsing proves

`Schema.parse schema raw` proves that this operation accepted the raw input and constructed its result through the
schema. `Schema.check schema value` proves the same checks for an already assembled value and re-runs a record
constructor where one exists.

Neither operation can change what ordinary F# construction permits. If this type is public, callers can bypass the
schema:

```fsharp
type Signup = { Email: string; Age: int }

let bypassed = { Email = ""; Age = 4 }
```

That is a language-level fact, not a missing validation call. Choose the representation according to the guarantee
the application needs:

- Use plain records for wire contracts and drafts. Treat them as untrusted outside a successful schema operation.
- Use private refined values for intrinsic field invariants such as non-blank names, positive quantities, or IDs
  with a required format.
- Use a private domain representation or complete smart constructor when cross-field invariants must hold for every
  value in application code.
- Use `Schema.check` at imports where an already assembled value arrived from a serializer, database mapper, plugin,
  or legacy API. It is not the normal constructor for a well-encapsulated domain type.
- Use contextual rules for facts that vary by operation or environment, such as “assignee belongs to this workspace”
  or “demo names are forbidden in production.”

The [trusted construction guide](trusted-construction.md) develops these options. The [refined values guide](refined-values.md)
shows fallible smart constructors inside schemas.

The [recommended patterns](patterns/) show complete module and project layouts for private aggregates, legal updates,
generated wire records, and schema-derived tests.

## Qualified and DSL forms

The qualified catalog is explicit and works well in application code. `open Axial.Schema.DSL` exposes the same
functions without their qualifiers inside a schema-definition module:

```fsharp
module SignupSchemas =
    open Axial.Schema
    open Axial.Schema.DSL

    let signup =
        recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
        |> field "email" _.Email (text |> constrainAll [ required; email ])
        |> field "age" _.Age (int |> constrain (atLeast 13))
        |> build
```

The DSL does not add a second grammar. `text`, `list`, `refine`, `field`, `parse`, and `check` delegate to the same
catalog and return the same types.
