---
weight: 50
title: Schema Integration
description: Apply refinements, conversions, and domain admission at structured boundaries.
type: docs
---


Schema describes structured input: fields, wire representations, path-aware failures, accumulation, and reconstruction.
A refinement supplies the value-level step that turns an already-decoded underlying value into an
invariant-carrying domain value. Schema can apply that refinement at a field boundary and report its failures at the
field's path.

`Axial.Refined` has no Schema dependency. Domain types can define refinements without choosing a wire format; an
application that uses `Axial.Schema` decides where those refinements participate in structured decoding and encoding.

If Schema is new to you, start with [Schema Getting Started]({{< relref "/schema/getting-started/" >}}) for fields,
record construction, and path-aware diagnostics. Then read [Refined Values in Schema]({{< relref
"/schema/refined-values/" >}}) for canonical field schemas, raw-value constraints, explicit refinement, and
schema-local restrictions. This page is the shorter API-oriented view of that integration.

## Refine a primitive schema

```fsharp
open Axial.Refined
open Axial.Schema

let quantitySchema : Schema<PositiveInt> =
    Schema.int
    |> Schema.refine PositiveInt.refinement
```

Parsing checks the underlying `int`, constructs `PositiveInt`, and reports failures at the schema path. Encoding and
checking project through `PositiveInt.Value`.

For an application type:

```fsharp
let emailSchema : Schema<ContactEmail> =
    Schema.text
    |> Schema.refine ContactEmail.refinement
```

A field block receives the refinement explicitly:

```fsharp
let signupSchema =
    schema<Signup> {
        field "email" _.Email {
            withSchema Schema.text
            refine ContactEmail.refinement
        }

        field "age" _.Age
        construct Signup.create
    }
```

## Choose the operation by meaning

- `Schema.refine refinement schema` constructs an invariant-carrying destination and retains refinement metadata.
- `Schema.convert forward backward schema` performs a total projected mapping.
- `Schema.tryConvert forward backward schema` performs a fallible projected mapping returning `SchemaError list`.
- `Schema.admit create project draftSchema` constructs a domain model from a structured draft while preserving fields.

```fsharp
let centsSchema : Schema<decimal> =
    Schema.int
    |> Schema.convert decimal int
```

```fsharp
let bookingSchema : Schema<Booking> =
    bookingDraftSchema
    |> Schema.admit Booking.create Booking.toDraft
```

For the complete progression from a raw field schema to a canonical refined field, continue to
[Refined Values in Schema]({{< relref "/schema/refined-values/" >}}).
