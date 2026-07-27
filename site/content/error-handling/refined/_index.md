---
weight: 40
title: Refined
type: docs
description: Construct invariant-carrying values from already-typed underlying values.
---


`Axial.Refined` constructs types whose private representation records that a check passed. It depends only on
`Axial.Check`.

```sh
dotnet add package Axial.Refined
```

```fsharp
open Axial.Check
open Axial.Refined
```

## Construct a supplied refined value

```fsharp
let quantity : Result<PositiveInt, CheckFailure list> =
    Refine.positiveInt 3

let name : Result<NonBlankString, CheckFailure list> =
    Refine.nonBlankString "Ada"

let tags : Result<NonEmptyList<string>, CheckFailure list> =
    Refine.nonEmptyList [ "fsharp"; "schema" ]
```

Read the canonical representation through the matching type module or member:

```fsharp
let printQuantity (quantity: PositiveInt) =
    printfn "%d" quantity.Value
```

## Define an application refinement

```fsharp
type CustomerId =
    private
    | CustomerId of int

module CustomerId =
    let value (CustomerId value) = value

    let refinement =
        Refinement.define
            (Constraint.greaterThan 0)
            CustomerId
            value

    let create value =
        Refinement.create refinement value
```

`Refinement.create` runs the constraint before invoking the total constructor. `Refinement.underlying` applies the
reverse projection. `Refinement.constraints` exposes portable metadata for Schema and other interpreters.

## Compose with parsing

Parsing changes representation; refinement admits a subset of an already-typed value. Keep both operations visible:

```fsharp
open Axial.Parse
open Axial.Result

type QuantityError =
    | InvalidInteger of ParseError
    | InvalidQuantity of CheckFailure list

let quantity raw =
    result {
        let! parsed = Parse.int raw |> Result.mapError InvalidInteger
        let! quantity = Refine.positiveInt parsed |> Result.mapError InvalidQuantity
        return quantity
    }
```

## Read next

1. [Parse](/error-handling/parse/) covers serialized primitive input.
2. [Built-in Refined Values](./catalog/) covers supplied domain types.
3. [Compose Parse and Refinement](./composition/) shows application-error mapping.
4. [Define Refined Types](./domain-values/) defines a private type and reusable refinement.
5. [Schema Integration](./schema/) applies refinements at structured boundaries.
