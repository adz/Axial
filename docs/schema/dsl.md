---
weight: 12
title: The Schema DSL
description: Open one module inside a schema definition and write fields without qualified prefixes.
---

# The Schema DSL

This page shows how to author schemas with the `Axial.Schema.DSL` open surface, and when to keep using the qualified
API instead.

The qualified pipeline is explicit but repetitive: the words users write most —
`SchemaConstraint.required`, `Schema.fieldWith`, `Value.text` — are dominated by their module prefixes. `DSL` is a
single curated module designed to be opened inside a schema definition, bringing exactly the authoring vocabulary into
scope bare:

```fsharp
open Axial.Schema

type Signup = { Email: string; Age: int; Note: string }

module SignupSchema =
    open Axial.Schema.DSL

    let private create email age note =
        { Email = email; Age = age; Note = note }

    let schema =
        recordFor<Signup, _> create
        |> text [ required; email ] "email" _.Email
        |> int  [ atLeast 13 ]      "age"   _.Age
        |> text []                  "note"  _.Note
        |> build
```

This builds exactly the same schema as the qualified pipeline — `DSL` contains aliases, not a second implementation —
so everything downstream (parsing, rules, codecs, JSON Schema, inspection) is unchanged.

## One Uniform Field Shape

Every field combinator takes the constraint list first; pass `[]` for an unconstrained field. There are no
`text`/`textWith` pairs — adding a first constraint later is an edit inside the brackets, not a rename.

- Primitives: `text`, `int`, `decimal`, `bool`, `date` (.NET only), `dateTime`, `guid`.
- Structure: `nested`, `many`, and `field` for an explicit `ValueSchema` such as a refined value.
- Entry and exit: `recordFor`, `build`, `buildResult`, `buildResultWith`.
- Constraints: `required`, `optional`, `email`, `trimmed`, `pattern`, `oneOf`, `notEqualTo`, `minLength`, `maxLength`,
  `lengthBetween`, `between`, `greaterThan`, `lessThan`, `atLeast`, `atMost`, `count`, `minCount`, `maxCount`,
  `countBetween`, `distinct`, and `withMessage`.

```fsharp
module OrderSchema =
    open Axial.Schema.DSL

    let private create address items total =
        { Address = address; Items = items; Total = total }

    let schema =
        recordFor<Order, _> create
        |> nested [ required ] "address" _.Address AddressSchema.schema
        |> many [ minCount 1 ] "items" _.Items LineItemSchema.schema
        |> decimal [ greaterThan 0m ] "total" _.Total
        |> build
```

## Open It Inside The Schema Module, Not At The Top Of The File

`DSL.int`, `DSL.decimal`, and `DSL.bool` shadow the FSharp.Core conversion functions of the same names. This is
deliberate — those words are the field vocabulary — and it is compile-time safe: a shadowed name used as a conversion
fails to type-check rather than misbehaving, and the originals stay reachable as `Operators.int` and friends. Keep the
shadowing contained by opening `DSL` inside the module that defines the schema, as in the examples above. Do not open
it at file or namespace level in general application code, and do not open it in code that mixes schema definitions
with numeric conversion work.

## When Not To Use It

- Outside schema definition modules, use the qualified `Schema.*` / `SchemaConstraint.*` / `Value.*` API; the DSL adds
  nothing there and the bare names cost clarity.
- When a field needs a custom `ValueSchema` built inline (refined values, unions), `field` accepts it explicitly —
  the DSL does not hide `Value`; `Value.refined` and `Value.union` remain qualified by design.
