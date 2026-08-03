---
weight: 1
title: "Result, Constraint, Parse, and Refined"
description: Install commands and a first look at each focused package.
type: docs
---


These packages perform explicit operations over individual values: compose `Result`, check a typed value, decode a
serialized primitive, or construct a refined domain value. For structured input with named fields, accumulated
path-aware errors, and reusable interpreters, use [Axial.Schema]({{< relref "/schema/" >}}). Schema applies the same
constraints and refinements at model boundaries rather than replacing them.

Install packages independently or install the dependency-only meta-package:

```bash
dotnet add package Axial.Result
dotnet add package Axial.Constraint
dotnet add package Axial.Parse
dotnet add package Axial.Refined
dotnet add package Axial.ErrorHandling
```

## Constrain a typed value

```fsharp
open Axial.Constraint

let nameCheck : Constraint<string> =
    Constraint.all [ Constraint.present; Constraint.maxLength 80 ]

let checkedName : Result<string, Violation> =
    "Ada" |> Result.guard nameCheck
```

A check returns `unit`. `Result.guard` returns the unchanged input after success.

## Decode serialized input

```fsharp
open Axial.Parse

let parsed : Result<int, ParseError> = Parse.int "42"
```

## Construct an invariant-carrying value

```fsharp
open Axial.Refined

let name : Result<NonBlankString, Violation> =
    Refine.nonBlankString "Ada"
```

## Compose through an application error

```fsharp
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

## Guides

- [Getting Started](/error-handling/getting-started/)
- [Constraint](../constraint/)
- [Result](../result/)
- [Parse](../parse/)
- [Refined](../refined/)
- [Schema Getting Started]({{< relref "/schema/getting-started/" >}})
- [Introductory Reference App](../reference-app/)
