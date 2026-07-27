---
weight: 30
title: Check DSL
type: docs
description: Write concise check modules and adapt check results without depending on Axial.Result.
---


`Axial.Check.CheckDSL` exposes common Check functions without the `Check.` prefix. Open it locally where a module's
purpose already makes the checking context clear:

```fsharp
open Axial.Check

module SignupChecks =
    open Axial.Check.CheckDSL

    let name : Check<string> =
        Check.all [ present; minLength 2; maxLength 80 ]

    let emailAddress : Check<string> =
        Check.all [ present; email; maxLength 254 ]

    let age : Check<int> =
        atLeast 13
```

The DSL changes vocabulary, not semantics. Every check still has the shape
`'value -> Result<unit, CheckFailure list>` and can be called directly.

## Type-directed presence names

`present`, `empty`, and `notEmpty` resolve from the checked value type. They cover strings, options, value options,
nullable values, and sequence-shaped values:

```fsharp
let requiredName : Check<string> = present
let selectedPlan : Check<string option> = present
let requiredItems : Check<Item list> = notEmpty
```

The input and destination types are already known at the call site. This dispatch selects a Check; it does not parse,
convert, or refine the value.

## Available short names

The DSL includes common text, numeric, collection, equality, and failure-mapping names:

```fsharp
minLength 2
maxLength 80
lengthBetween 2 80
exactLength 6
email
matches "^[A-Z]+$"
oneOf [ "draft"; "published" ]
atLeast 0
atMost 100
positive
minCount 1
maxCount 10
countBetween 1 10
equalTo expected
notEqualTo forbidden
mapFailure mapper
```

Some names deliberately keep their `Check.` qualification because unqualified versions would shadow common F#
operations: `Check.not`, `Check.contains`, `Check.distinct`, `Check.all`, `Check.any`, `Check.length`, and
`Check.between`.

## Preserve the input with guard

A Check returns `Ok ()`. `guard` runs it and returns the original input after success:

```fsharp
open Axial.Check.CheckDSL

let checkedName : Result<string, CheckFailure list> =
    "Ada" |> guard present
```

This makes checks convenient inside a value-preserving Result pipeline:

```fsharp
let normalizeCheckedName raw =
    raw.Trim()
    |> guard (Check.all [ present; maxLength 80 ])
```

Normalization happens explicitly before checking; `guard` itself never changes the value.

## Assign an application error with orError

`orError` replaces any Check failure list with one application error:

```fsharp
type SignupError =
    | NameRequired

let requireName value : Result<string, SignupError> =
    value
    |> guard present
    |> orError NameRequired
```

Use this when the application does not need the individual `CheckFailure` values.

## Retain details with mapError

`mapError` preserves the failure list by passing it to an application error constructor:

```fsharp
type SignupError =
    | InvalidEmail of CheckFailure list

let requireEmail value : Result<string, SignupError> =
    value
    |> guard (Check.all [ present; email; maxLength 254 ])
    |> mapError InvalidEmail
```

This is useful when rendering, logging, or tests need the structured reasons.

## Package boundary

`guard`, `orError`, and `mapError` are small structural adapters in `Axial.Check`. They are available without an
`Axial.Result` dependency. Code that already uses `Axial.Result` can instead use the corresponding `Result.guard`,
`Result.orError`, and `Result.mapError` functions.

The two surfaces serve different import styles; they do not introduce a separate workflow type.

## When to use the DSL

Open `CheckDSL` inside focused check-definition modules or short boundary functions. Keep `Check.` qualification where
several domains share a scope or where the qualified name makes composition easier to scan.

Continue to [Constraints](../constraints/) when checks must also carry metadata for Refined and Schema.
