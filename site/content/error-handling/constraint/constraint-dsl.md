---
weight: 10
title: Constraint DSL
type: docs
description: Write concise constraint modules and adapt results without depending on Axial.Result.
---


`Axial.Constraint.ConstraintDSL` exposes the common constructors without the `Constraint.` prefix. Open it locally
where a module's purpose already makes the context clear:

```fsharp
open Axial.Constraint

module SignupRules =
    open Axial.Constraint.ConstraintDSL

    let name : Constraint<string> =
        Constraint.all [ present; minLength 2; maxLength 80 ]

    let emailAddress : Constraint<string> =
        Constraint.all [ present; email; maxLength 254 ]

    let age : Constraint<int> =
        atLeast 13

    let handle : Constraint<string> =
        Constraint.all [ present; alphanumeric; noneOf [ "admin"; "root" ] ]
```

The DSL changes vocabulary, not semantics. Every name here returns the same `Constraint<'value>` the qualified name
returns; it is optional shorthand, not another abstraction.

## Type-directed names

`present`, `blank`, `optional`, and the size family pick their shape from the type they are used at, so most of the
time they just work:

```fsharp
let name : Constraint<string> = Constraint.all [ present; maxLength 80 ]
let tags : Constraint<Item list> = Constraint.all [ atLeastOne; Constraint.distinct ]

Schema.text |> Schema.constrain present
```

In each of those the surrounding type is already known, and the name resolves against it.

The one case that needs help is a binding whose only type information *is* the annotation — which is also the
central story of naming a reusable rule. Dispatch runs on the return type, so without the annotation the compiler
has nothing to select on:

```fsharp
let requiredName : Constraint<string> = present
let selectedPlan : Constraint<string option> = present
let requiredItems : Constraint<Item list> = minLength 1
```

This selects a constraint; it does not parse, convert, or refine anything.

## The catalogue

| Family | Names |
| --- | --- |
| Presence | `present`, `blank`, `optional` |
| Size | `minLength`, `maxLength`, `lengthBetween`, `single`, `atLeastOne`, `atMostOne`, `moreThanOne` |
| Comparison | `equalTo`, `notEqualTo`, `greaterThan`, `lessThan`, `atLeast`, `atMost` |
| Sign | `positive`, `nonNegative`, `negative`, `nonPositive` |
| Membership | `oneOf`, `noneOf`, `notContains` |
| Format | `email`, `trimmed`, `numeric`, `alphanumeric`, `pattern` |
| Number | `multipleOf`, `finite`, `finite32` |
| Opaque | `notWith`, `custom`, `customLocalized`, `customWith`, `contramap` |
| Other | `describe`, `test`, `guard`, `orError`, `mapError` |

The sign and size names are spellings, not new primitives: `positive` is `greaterThan 0` at the value's own numeric
type, and `atLeastOne` is `minLength 1`. Each builds the same atom its general form builds, so inspection, export,
and generation treat them identically.

## Names left off, and why

Some constructors are deliberately absent because they shadow names the same validation code is likely to need:

| Left off | Reason | Reach for |
| --- | --- | --- |
| `all`, `any`, `contains`, `distinct`, `length`, `between` | shadow core F# operations | `Constraint.all`, `Constraint.contains`, … |
| `check` | shadows `Schema.check` | `Constraint.check` |

The omissions are driven by collision alone, which is why `notContains` is exported although `contains` is not — no
core operation is named `notContains`. `test` has no collision either and is exported.

## Result adapters

`guard`, `orError`, and `mapError` are small structural adapters matching the corresponding `Result` operations. They
live here because `Axial.Constraint` does not depend on `Axial.Result`, so a constraint pipeline can retain its input
and finish with the application's own error type without adding a package reference:

```fsharp
open Axial.Constraint.ConstraintDSL

let requiredName (value: string) =
    value |> guard present |> orError NameRequired

let quantity value =
    value |> guard (atLeast 1) |> mapError InvalidQuantity
```

`Result.guard` provides the same value-preserving operation for code that already uses `Axial.Result`.
