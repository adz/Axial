---
weight: 30
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
```

The DSL changes vocabulary, not semantics. Every name here returns the same `Constraint<'value>` the qualified name
returns; it is optional shorthand, not another abstraction.

## Type-directed names

`present`, `blank`, and the size family resolve from the constraint's own type. They cover text, options, value
options, nullables, lists, arrays, and maps:

```fsharp
let requiredName : Constraint<string> = present
let selectedPlan : Constraint<string option> = present
let requiredItems : Constraint<Item list> = minLength 1
```

Annotate the binding. The dispatch runs on the *return* type, so without an annotation the compiler has nothing to
select on. This selects a constraint; it does not parse, convert, or refine anything.

## Names left off, and why

Some constructors are deliberately absent because they shadow names the same validation code is likely to need:

| Left off | Reason | Reach for |
| --- | --- | --- |
| `all`, `any`, `contains`, `distinct`, `length`, `between` | shadow core F# operations | `Constraint.all`, `Constraint.contains`, … |
| `check` | shadows `Schema.check` | `Constraint.check` |

`test` has no such collision and is exported.

## Result adapters

`guard`, `orError`, and `mapError` are small structural adapters matching the corresponding `Result` operations. They
live here because `Axial.Constraint` does not depend on `Axial.Result`, so a constraint pipeline can retain its input
and finish with the application's own error type without adding a package reference:

```fsharp
open Axial.Constraint.ConstraintDSL

let requiredName value =
    value |> guard present |> orError NameRequired

let quantity value =
    value |> guard (atLeast 1) |> mapError InvalidQuantity
```

`Result.guard` provides the same value-preserving operation for code that already uses `Axial.Result`.
