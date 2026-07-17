---
title: Test Schema Guarantees
weight: 50
description: Derive accepted values from a schema and test transitions, codecs, and mappings against the same rules.
---

# Test schema guarantees

The compiler checks type shape and visibility. It cannot prove that a constructor contains the right business rule or
that a total transition preserves it.

A **schema guarantee** here means the behavior established by a successful `Schema.parse` or `Schema.check` call. It is
not a permanent property of a public record that callers can construct elsewhere.

A **transition** is a function that changes one accepted domain value into another. A total transition returns the new
value directly and therefore claims it cannot break the type's rules.

Use generated examples to check those claims near the owning module.

## Generate accepted models

`Axial.Schema.Testing` is a test-only FsCheck adapter. `SchemaGen.model` derives values by generating raw input, parsing
it, and immediately checking the result.

```fsharp
open Axial.Schema.Testing
open FsCheck.FSharp

let bookingGenerator =
    SchemaGen.model Booking.schema
    |> Result.defaultWith (failwithf "%A")

let bookings = Gen.sample 100 bookingGenerator
```

Keep the package in the test project; it is not a runtime dependency.

## Check total transitions

A transition returning `Booking` claims it always preserves the aggregate invariant.

```fsharp
let shiftedBookings =
    bookings
    |> Array.map (Booking.shift 10)

let allRemainValid =
    shiftedBookings
    |> Array.forall (Schema.check Booking.schema >> Result.isOk)
```

Use an FsCheck property in a real test suite so it explores different values and shift amounts.

## Check codecs and mappings

For generated domain values, test that serialization and decoding preserve the value expected by the application.

For wire-to-domain mappings, generate wire values and assert that accepted values produce a domain value that passes its
own constructor or schema.

Do not derive all test input from the schema. Schema-derived generation is good at accepted values. Keep separate broad
or hand-picked inputs for malformed data, constraint edges, and constructor failures.

## Supply custom generators when needed

Patterns, custom constraints, and application-specific distributions cannot always be reversed automatically.
`SchemaGen.rawWith` accepts generators keyed by field path.

```fsharp
let overrides =
    Map.ofList [ "reference", Gen.constant (RawInput.Scalar "BK-42") ]

let rawGenerator =
    SchemaGen.rawWith overrides bookingWireSchema
    |> Result.defaultWith (failwithf "%A")
```

The adapter reports `UnsupportedConstraint` instead of guessing a generator that might disagree with the schema.
