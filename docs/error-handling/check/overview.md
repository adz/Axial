---
weight: 10
title: Using Check
description: Checks, portable constraints, failure accumulation, and Result.guard.
---

# Using Check

```fsharp
open Axial.Check

type Check<'value> =
    'value -> Result<unit, CheckFailure list>
```

A check tests an existing typed value. Successful checks return `Ok ()`; they never trim, normalize, or replace the
input.

## Name reusable checks

```fsharp
let nameCheck : Check<string> =
    Check.all [ Check.String.present; Check.String.lengthBetween 2 40 ]

nameCheck "Ada"
// Ok ()
```

`Check.all` runs every child against the same original value and accumulates failures. `Check.any` succeeds when one
alternative succeeds. `Check.not` inverts a check, and `Check.mapFailure` changes its failure values.

Open [`Axial.Check.CheckDSL`](../check-dsl/) inside a check-definition module when unqualified names improve readability:

```fsharp
open Axial.Check.CheckDSL

let emailCheck : Check<string> =
    Check.all [ present; email; maxLength 254 ]
```

## Keep the value with guard

The Check DSL provides `guard` beside `orError` and `mapError`:

```fsharp
open Axial.Check.CheckDSL

let checkedName : Result<string, CheckFailure list> =
    "Ada" |> guard nameCheck

let requiredName =
    "Ada" |> guard present |> orError NameRequired
```

`CheckDSL.guard`, `orError`, and `mapError` are small structural adapters implemented in `Axial.Check` because that
package does not depend on `Axial.Result`. `Result.guard` provides the same value-preserving operation for code that
already uses `Axial.Result`.

Map failures at an application boundary:

```fsharp
type SignupError = InvalidName of CheckFailure list

let validateName name =
    name
    |> Result.guard nameCheck
    |> Result.mapError InvalidName
```

## Portable constraints

A `Constraint<'value>` combines executable behavior and metadata. See [Constraints](../constraints/) for why the
metadata matters and how Refined and Schema consume it.

```fsharp
let maximumNameLength : Constraint<string> =
    Constraint.maxLength 80
```

The complete [Constraints guide](../constraints/) covers inspection, custom codes, refined domain values, and Schema
interpreters.

A constraint exposes both forms:

```fsharp
let details : ConstraintDetails =
    Constraint.details maximumNameLength

let check : Check<string> =
    Constraint.check maximumNameLength
```

Built-in constraints include text formats and lengths, ordered bounds, collection counts, distinctness, multiples,
and closed choices. Metadata arguments use the closed `ConstraintArgument` union rather than `obj`.

Use the same constraint in the next layers:

- [Define a refined type]({{< relref "/error-handling/refined/domain-values/" >}}) when successful checking should produce an invariant-carrying type.
- [Apply a refinement in Schema]({{< relref "/schema/refined-values/" >}}) when structured input needs paths, accumulated diagnostics, reconstruction, and wire metadata.

Custom metadata is author-declared:

```fsharp
let even : Constraint<int> =
    Constraint.define
        "even"
        Seq.empty
        (fun value ->
            if value % 2 = 0 then Ok ()
            else Error [ CheckFailure.Custom "even" ])
```

Built-in codes are reserved.

## Structured failures

`CheckFailure` distinguishes required values, formats, lengths, ordered ranges, collection counts, choices,
duplicates, and custom codes. Render failures with `CheckFailure.describe` or `CheckFailure.describeAll`, or pattern
match when the application needs structured behavior.

## Check or extract

Checks preserve shape by returning `unit`. Extraction changes shape and belongs to Result or an explicit conversion:

| Prove a fact | Extract a value |
| --- | --- |
| `Check.Option.some` | `Result.someOr` |
| `Check.ValueOption.some` | `Result.valueSomeOr` |
| `Check.Nullable.hasValue` | `Result.nullableOr` |
| `Check.Result.ok` | `Result.okOr` |
| `Check.Seq.notEmpty` | `Result.headOr` |
| `Check.Seq.count 1` | `Refine.exactlyOne` |

Use [Predicates](../predicates/) when a local branch needs `bool` rather than structured failures.
