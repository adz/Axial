---
weight: 10
title: Getting Started
description: Declare fields, parse structured input, and inspect every path-aware failure.
type: docs
---


Install Schema:

```bash
dotnet add package Axial.Schema
```

```fsharp
open Axial
open Axial.Schema
open type Axial.Schema.Syntax
```

## Declare the model

```fsharp
type Signup =
    { Email: string
      Age: int }

let signupSchema =
    schema<Signup> {
        field "email" _.Email {
            constrain Constraint.required
            constrain Constraint.email
        }

        field "age" _.Age {
            constrain (Constraint.atLeast 18)
        }

        construct (fun email age ->
            { Email = email
              Age = age })
    }
```

Each `field` records a wire name, getter, value schema, and local rules. The final constructor receives the fields in
declaration order. The compiler checks its argument types and final result.

The getter normally resolves the field schema automatically. `string` resolves `Schema.text`; `int` resolves
`Schema.int`; options, lists, maps, built-in refined values, and application types with a static `Schema` member can
also resolve canonical schemas.

## Parse structured input

`Data` is a source-neutral input tree:

```fsharp
let input =
    Data.ofNameValues [
        "email", "ada@example.org"
        "age", "36"
    ]

let parsed : Result<Signup, SchemaErrors> =
    Schema.parse signupSchema input
```

No `Signup` is returned unless both fields parse, every field rule succeeds, and the constructor succeeds.

## Read every failure

Independent fields are all interpreted. Errors contain complete paths:

```fsharp
match Schema.parse signupSchema badInput with
| Ok signup ->
    save signup
| Error errors ->
    for issue in SchemaErrors.toList errors do
        printfn "%s: %s"
            (Path.format issue.Path)
            (SchemaError.render issue.Error)
```

Schema supplies paths from structure. Application code does not repeat `"email"`, nested object names, list indexes, or
map keys around separate validation expressions.

## Refine a field

A raw boundary value and a domain field can have different types:

```fsharp
type ContactEmail =
    private
    | ContactEmail of string

// ContactEmail.refinement and its static Refinement contribution are defined beside the type.

type Contact =
    { Email: ContactEmail }

let contactSchema =
    schema<Contact> {
        field "email" _.Email {
            withSchema Schema.text
            constrain Constraint.required
            refine
        }

        construct (fun email -> { Email = email })
    }
```

`withSchema` starts the field as `Schema<string>`. `refine` resolves the contributed
`Refinement<string,ContactEmail>` from the raw schema and getter type. The constructor receives `ContactEmail`.

See [Define Refined Types]({{< relref "/error-handling/refined/domain-values/" >}}) for the complete application type.

## Check an existing value

`Schema.check` covers drafts or imported values that did not come through `Schema.parse`:

```fsharp
let checkedDraft : Result<Signup, SchemaErrors> =
    Schema.check signupSchema draft
```

It runs field constraints, refinements, executable validation, nested schemas, and checked construction again.

## Continue

- [Schema Syntax](./syntax/)
- [Field Blocks and Plain Functions](./field-desugaring/)
- [Input Sources](./input-sources/)
- [Refined Values](./refined-values/)
- [JSON Codec](./json-codec/)
