---
weight: 30
title: Compose Parse and Refinement
description: Map parse and check failures into an application error and compose with result {}.
type: docs
---


Parsing and refinement have different failure types because they answer different questions. Define an application
error that preserves that distinction, then compose with `result { }`.

```fsharp
open Axial.Constraint
open Axial.Parse
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

## Compose several values

```fsharp
type OrderInputError =
    | InvalidQuantityText of ParseError
    | InvalidQuantity of Violation
    | InvalidSku of Violation

let orderLine rawQuantity rawSku =
    result {
        let! parsed = Parse.int rawQuantity |> Result.mapError InvalidQuantityText
        let! quantity = parsed |> Constraint.guard (Constraint.greaterThan 0) |> Result.mapError InvalidQuantity
        let! sku = Refine.nonBlankString rawSku |> Result.mapError InvalidSku
        return quantity, sku
    }
```

Every bind names the operation and the error translation. The application decides whether two failures share a case or
remain distinct.

## Reuse a refinement

Application-defined types expose a named refinement value:

```fsharp
let customerId raw =
    result {
        let! parsed = Parse.int raw |> Result.mapError InvalidCustomerIdText
        let! id = Refinement.create CustomerId.refinement parsed |> Result.mapError InvalidCustomerId
        return id
    }
```

Use ordinary functions when additional configuration is required. `Parse.optional`, `Choice.orElse`, and
`Refinement.create` all compose through standard `Result` functions.

Continue with [Define Refined Types](../domain-values/).
