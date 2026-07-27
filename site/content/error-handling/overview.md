---
weight: 1
title: "Result, Check, Parse, and Refined"
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
dotnet add package Axial.Check
dotnet add package Axial.Parse
dotnet add package Axial.Refined
dotnet add package Axial.ErrorHandling
```

## Check a typed value

```fsharp
open Axial.Check

let nameCheck : Check<string> =
    Check.all [ Check.String.present; Check.String.maxLength 80 ]

let checkedName : Result<string, CheckFailure list> =
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

let quantity : Result<PositiveInt, CheckFailure list> =
    Refine.positiveInt 42
```

## Compose through an application error

```fsharp
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

## Guides

- [Getting Started](/error-handling/getting-started/)
- [Check](../check/)
- [Result](../result/)
- [Parse](../parse/)
- [Refined](../refined/)
- [Schema Getting Started]({{< relref "/schema/getting-started/" >}})
- [Introductory Reference App](../reference-app/)
