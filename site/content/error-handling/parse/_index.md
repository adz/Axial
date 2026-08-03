---
weight: 30
title: Parse
type: docs
notoc: true
description: Decode serialized strings into primitive F# values.
---


`Axial.Parse` is an independent package. Each named parser changes representation and returns
`Result<'value, ParseError>`.

```sh
dotnet add package Axial.Parse
```

```fsharp
open Axial.Parse

let count : Result<int, ParseError> = Parse.int "12"
let enabled : Result<bool, ParseError> = Parse.bool "true"
let price : Result<decimal, ParseError> = Parse.decimal "19.95"
let id : Result<System.Guid, ParseError> =
    Parse.guid "89d45a4b-f634-4db0-9a41-7e8461957be1"
```

Numeric parsing uses invariant culture. Missing text, malformed text, and values outside the destination range remain
distinct `ParseError` cases.

## Optional input

```fsharp
Parse.optional Parse.int None
// Ok None

Parse.optional Parse.int (Some "42")
// Ok (Some 42)

Parse.optional Parse.int (Some "bad")
// Error (InvalidFormat ("int", "bad"))
```

`Parse.optionalOr` supplies a value only when the input is absent:

```fsharp
Parse.optionalOr 80 Parse.int None
// Ok 80

Parse.optionalOr 80 Parse.int (Some "443")
// Ok 443

Parse.optionalOr 80 Parse.int (Some "bad")
// Error (InvalidFormat ("int", "bad"))
```

The fallback does not recover from malformed input. It distinguishes an omitted value from an invalid supplied value.

## Combined optional helpers

Named helpers combine the common primitive parser with `optional`:

```fsharp
Parse.intOption (Some "42")
// Ok (Some 42)

Parse.boolOption None
// Ok None

Parse.decimalOption (Some "12.5")
// Ok (Some 12.5M)

Parse.guidOption (Some "89d45a4b-f634-4db0-9a41-7e8461957be1")
// Ok (Some 89d45a4b-f634-4db0-9a41-7e8461957be1)
```

The corresponding defaulting helpers combine the primitive parser with `optionalOr`:

```fsharp
Parse.intOrDefault 80 None
// Ok 80

Parse.boolOrDefault false (Some "true")
// Ok true

Parse.decimalOrDefault 5.5M (Some "bad")
// Error (InvalidFormat ("decimal", "bad"))
```

Use `*Option` when absence should remain `None`. Use `*OrDefault` when absence should produce a concrete value. Both
forms preserve errors from malformed present text.

## Parse, then refine

Parsing changes representation; refinement checks an already-typed value and constructs a domain type. See
[Refined values]({{< relref "/error-handling/refined/" >}}) for the refinement model and its built-in types.

```fsharp
open Axial.Constraint
open Axial.Refined
open Axial.Result

type QuantityError =
    | InvalidInteger of ParseError
    | InvalidQuantity of Violation

let quantity raw =
    result {
        let! parsed = Parse.int raw |> Result.mapError InvalidInteger
        let! quantity = parsed |> Constraint.guard (Constraint.greaterThan 0) |> Result.mapError InvalidQuantity
        return quantity
    }
```

The [Parse API reference]({{< relref "/error-handling/reference/parse/" >}}) lists every parser.
