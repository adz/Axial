---
weight: 2
title: Getting Started
type: docs
description: One schema describing boundary input, domain construction, checking, codecs, and metadata.
---

# Schema: one description, several interpreters

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
Schema.list<string>()               // Schema<string list>
RefinedSchemas.nonBlankString       // Schema<NonBlankString>
```

Record schemas use those values directly:

```fsharp
open Axial.Schema
open Axial.Schema.Syntax

type Signup =
    { Email: string
      Age: int }

let signupSchema : Schema<Signup> =
    Schema.define<Signup>
    |> field "email" _.Email
    |> constrain emailFormat
    |> field "age" _.Age
    |> constrain (atLeast 13)
    |> construct (fun email age -> { Email = email; Age = age })
```

`field` infers built-in schemas and the canonical schema declared by a user-owned or generated type. Inference is
recursive through `option`, `list`, and `Map<string, _>`. See [Schema Syntax](syntax.md) for the custom
`static member Schema` convention. Use `fieldWith` when a field needs an explicit local schema or its type cannot
declare a canonical schema.

## Parse boundary input

```fsharp
let raw =
    RawInput.Object(
        Map.ofList
            [ "email", RawInput.Scalar "ada@example.com"
              "age", RawInput.Scalar "42" ])

match (Schema.parse signupSchema raw) with
| Ok signup -> printfn "%s" signup.Email
| Error diagnostics ->
    diagnostics
    |> Axial.Validation.Diagnostics.flatten
    |> List.iter (SchemaError.renderDiagnostic >> printfn "%s")
```

Parsing performs shape conversion, runs constraints, and invokes the declared record constructor. Errors retain their
paths. Use `Schema.parseRetainingInput` when form redisplay or auditing also needs the original `RawInput`.

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
  or external integration. It is not the normal constructor for a well-encapsulated domain type.
- Use contextual rules for facts that vary by operation or environment, such as “assignee belongs to this workspace”
  or “demo names are forbidden in production.”

The [trusted construction guide](trusted-construction.md) develops these options. The [refined values guide](refined-values.md)
shows fallible smart constructors inside schemas.

The [recommended patterns](patterns/) show complete module and project layouts for private aggregates, legal updates,
generated wire records, and schema-derived tests.

## Schema-definition modules

Open `Axial.Schema.Syntax` inside a schema-definition module to use fields, typed constraints, and closing constructors:

```fsharp
module SignupSchemas =
    open Axial.Schema
    open Axial.Schema.Syntax

    let signup =
        Schema.define<Signup>
        |> field "email" _.Email
        |> constrain emailFormat
        |> field "age" _.Age
        |> constrain (atLeast 13)
        |> construct (fun email age -> { Email = email; Age = age })
```

Primitive and composite value schemas remain qualified through `Schema.text`, `Schema.list<'item>()`, `Schema.refine`, and the
rest of the `Schema` catalog. `field`, `fieldWith`, `constrain`, `construct`, and `constructResult` form the object-shape
pipeline.
