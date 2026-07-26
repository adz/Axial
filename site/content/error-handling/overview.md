---
weight: 1
title: "Result, Check, and Refined"
description: Install commands and a first look at each focused package.
type: docs
---


Install any focused package directly, or install `Axial.ErrorHandling` for all three:

```bash
dotnet add package Axial.Result
dotnet add package Axial.Check
dotnet add package Axial.Refined
dotnet add package Axial.ErrorHandling   # installs all three
```

## Result and Check

Use ordinary `Result<'value,'error>` for operations that stop at the first failure. `Check<'value>` describes reusable
rules over one typed value and returns the original value after success.

```fsharp
open Axial.Check
open Axial.Check.CheckDSL

let validateName name =
    name
    |> minLength 3
    |> orError NameTooShort
```

`result { }` keeps dependent steps linear:

```fsharp
open Axial.Result

result {
    let! quantity = Parse.int rawQuantity |> Result.mapError InvalidQuantity
    do! quantity > 0 |> Result.requireTrue QuantityMustBePositive
    return quantity
}
```

## Refined values

Parse text with a named parser:

```fsharp
let parsed : Result<int, ParseError> =
    Parse.int "42"
```

Refine an ordinary value with a named constructor:

```fsharp
let quantity : Result<PositiveInt, RefinementError> =
    Refine.positiveInt 42
```

See [Refined](../refined/) for the supplied types, dependent construction, and application-defined refined types.

## Guides

- [Getting Started](../getting-started/)
- [Check](../check/)
- [Result Builder](../result-builder/)
- [Refined](../refined/)
- [Introductory Reference App](../reference-app/)
