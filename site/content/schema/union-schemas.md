---
weight: 45
title: Union Schemas
type: docs
description: Tagged discriminated unions as schema fields.
---


This page shows how to describe F# discriminated unions as schema fields with explicit tags and payload schemas.

Use union schemas when a domain field naturally has a small set of cases, and each case carries a different payload
shape. The raw input convention is an object with a discriminator field and a payload field:

```fsharp
{ type = "card"; value = { number = "4242" } }
{ type = "invoice"; value = "inv-42" }
```

## Define Cases

Each case supplies a tag, a constructor, a payload extractor, and a payload schema:

```fsharp
open Axial.Refined
open Axial.Schema
open Axial.Schema.DSL

type CardDetails =
    {
        Number: NonBlankString
    }

type Payment =
    | Card of CardDetails
    | Invoice of Slug

let cardSchema =
    recordFor<CardDetails, _> (fun number -> { Number = number })
    |> field "number" _.Number RefinedSchemas.nonBlankString
    |> build

let paymentValue =
    union
        "type"
        "value"
        [ UnionCase.create "card" Card (function Card details -> Some details | _ -> None) cardSchema
          UnionCase.create "invoice" Invoice (function Invoice slug -> Some slug | _ -> None) RefinedSchemas.slug ]
```

The extractor is what lets validation and metadata/codecs inspect an existing trusted union without reflection.

## Use As A Field

Union value schemas are ordinary `Schema<'value>` values, so use `Schema.field`:

```fsharp
open Axial.Schema.DSL
type Checkout =
    {
        Payment: Payment
    }

let checkoutSchema =
    recordFor<Checkout, _> (fun payment -> { Payment = payment })
    |> field "payment" _.Payment paymentValue
    |> build
```

Parsing attaches diagnostics to the discriminator or payload path:

```fsharp
let raw =
    RawInput.Object(
        Map.ofList [
            "payment",
            RawInput.Object(
                Map.ofList [
                    "type", RawInput.Scalar "card"
                    "value", RawInput.Object(Map.ofList [ "number", RawInput.Scalar "" ])
                ]
            )
        ]
    )

let parsed = Schema.parse checkoutSchema raw
let errors = parsed.Errors
```

The failing payload field reports at `payment.value.number`. An unknown tag reports at `payment.type`.

## Metadata

`Inspect` exposes union schemas as `SchemaShape.Union` with the discriminator field, payload field, and per-case payload
descriptions. Prototype JSON Schema interpreters can lower this to `oneOf`, while UI interpreters can render the tag
selector and then render the selected payload schema.
