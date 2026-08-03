---
title: "Values: admit a value, or say why not"
linkTitle: Values
type: docs
notoc: true
description: Reusable value constraints, invariant-carrying domain types, and named parsers for serialized primitives.
weight: 4
menu:
  main:
    weight: 2
---


**Values is a grouping, not a package.** There is no `Axial.Values` on NuGet and no `Axial.Values` namespace. It is
the name for three packages that answer the same question — *may this value into my program?* — each installed on
its own:

| Package | Answers | Returns |
| --- | --- | --- |
| [`Axial.Constraint`](./constraint/) | Is this typed value acceptable? | `Result<unit, Violation>` |
| [`Axial.Refined`](./refined/) | Can I build a type that records the answer? | `Result<'refined, Violation>` |
| [`Axial.Parse`](./parse/) | What does this serialized text decode to? | `Result<'value, ParseError>` |

```sh
dotnet add package Axial.Constraint   # reusable checks and portable constraints
dotnet add package Axial.Refined      # invariant-carrying domain values
dotnet add package Axial.Parse        # serialized primitive decoding
```

`Axial.Refined` depends on `Axial.Constraint`. `Axial.Parse` depends on neither. None of them depends on
`Axial.Result`: every one returns the standard F# `Result`, so they compose with
[`Axial.Result`]({{< relref "/result/" >}}), FsToolkit.ErrorHandling, or your own helpers — your choice, not the
library's.

## The three in one function

```fsharp
open Axial.Parse
open Axial.Constraint
open Axial.Refined

type Quantity =
    private Quantity of int

    static member Refinement =
        Refinement.define (Constraint.between 1 999)

let quantity (raw: string) =
    raw
    |> Parse.int                                   // Result<int, ParseError>
    |> Result.mapError InvalidInteger
    |> Result.bind (Refine.value<Quantity> >> Result.mapError InvalidQuantity)
```

`Parse` changes representation. `Constraint` describes what is acceptable. `Refined` makes the successful check
part of the type, so nothing downstream re-checks it.

## Learn and solve tasks

### Constraint

- [Overview](./constraint/) — one reusable description of valid values.
- [Reusable constraints](./constraint/constraints/)
- [Constraint composition](./constraint/constraint-dsl/)
- [Working with violations](./constraint/violations/)
- [Contextual localization](./constraint/localization/) and [adding a language](./constraint/adding-a-language/)
- [Under Fable](./constraint/fable/)

### Refined

- [Overview](./refined/) — types that record a successful check.
- [Define refined types](./refined/domain-values/)
- [Built-in refined values](./refined/catalog/)
- [Composition](./refined/composition/)
- [Refined fields in a schema](./refined/schema/)

### Parse

- [Overview](./parse/) — named parsers for serialized primitive values.

### Across all three

- [Getting started](./getting-started/)
- [Tutorial: constraints and Result](./tutorials/constraint-result/)
- [Introductory reference app](./reference-app/)
- [API reference]({{< relref "/values/reference/" >}})

## Related

[`Axial.Result`]({{< relref "/result/" >}}) composes the failures these packages return into one application error
type. [`Axial.Schema`]({{< relref "/schema/" >}}) applies the same constraints and refinements at declared fields
of a structured input, and returns accumulated diagnostics carrying the path of each failure.
