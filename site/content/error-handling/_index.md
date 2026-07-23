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

Use a refined type when later code must know construction succeeded:

```fsharp
let quantity : Result<PositiveInt, RefinementError> =
    Refine.from parsedQuantity
```

Application types can contribute the same bidirectional `Refinement<'raw,'value>` to `Refine.from`, `refine { }`, and
Schema. See [Define Refined Types](./refined/domain-values/).

## Accumulated boundary failures

Schema owns path-aware accumulation. It already knows object field names, list indexes, map keys, nested structure, and
the constructor. `Schema.parse` and `Schema.check` return `SchemaErrors` without requiring application code to repeat
paths.

Error Handling remains independent of Schema and Flow.

## Guides

- [Getting Started](./getting-started/)
- [Checks](./checks/)
- [Result Builder](./result-builder/)
- [Refined](./refined/)
- [Introductory Reference App](./reference-app/)
