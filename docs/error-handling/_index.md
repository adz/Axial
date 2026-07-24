---
title: "Error Handling: Result, checks, and refined values"
linkTitle: Error Handling
type: docs
notoc: true
description: Fail-fast results, reusable constraints, and domain values that record successful construction.
weight: 6
menu:
  main:
    weight: 4
---

# Error Handling

`Axial.ErrorHandling` installs two focused packages:

| Package | Use it for | Documentation |
| --- | --- | --- |
| `Axial.Result` | Fail-fast Results, reusable checks, predicates, and `result {}` | [Result](./result/) |
| `Axial.Refined` | Parsing and constructing values whose types record successful checks | [Refined](./refined/) |

Install either focused package directly, or install `Axial.ErrorHandling` for both.

## Result and Check

Use ordinary `Result<'value,'error>` for operations that stop at the first failure. `Check<'value>` describes reusable
rules over one typed value and returns the original value after success.

```fsharp
open Axial.ErrorHandling
open Axial.ErrorHandling.CheckDSL

let validateName name =
    name
    |> minLength 3
    |> Result.orError NameTooShort
```

`result { }` keeps dependent steps linear:

```fsharp
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

See [Refined](./refined/) for the supplied types, dependent construction, and application-defined refined types.

## Guides

- [Getting Started](./getting-started/)
- [Checks](./checks/)
- [Result Builder](./result-builder/)
- [Refined](./refined/)
- [Introductory Reference App](./reference-app/)
