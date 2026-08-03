---
weight: 10
title: Constraint and Result Tutorial
description: Apply reusable constraints and compose the values they keep through result { }.
type: docs
---


This tutorial validates a signup request and maps each `Violation` into an application error.

```fsharp
open Axial.Constraint
open Axial.Result

type SignupRequest =
    { Name: string
      Email: string
      Age: int
      AcceptedTerms: bool }

type SignupError =
    | TermsNotAccepted
    | InvalidName of Violation
    | InvalidEmail of Violation
    | InvalidAge of Violation

type Signup =
    { Name: string
      Email: string
      Age: int }
```

## Define the rules

A `Constraint<'value>` is a value, not a function. Annotate the binding: the catalogue's inline members pick their
shape from the type they are used at, and the annotation is the only type information a standalone binding has.

```fsharp
let name : Constraint<string> =
    Constraint.all [ Constraint.present; Constraint.lengthBetween 2 40 ]

let email : Constraint<string> =
    Constraint.all [ Constraint.present; Constraint.email ]

let age : Constraint<int> =
    Constraint.atLeast 13
```

These are reusable declarations. The same values work unchanged in a `Refinement` and in a `Schema`.

## Run them, keeping the value

`Constraint.guard` runs a constraint and returns the input on success, so the checked value flows onward:

```fsharp
let validateName (request: SignupRequest) =
    request.Name
    |> Constraint.guard name
    |> Result.mapError InvalidName

let validateEmail (request: SignupRequest) =
    request.Email
    |> Constraint.guard email
    |> Result.mapError InvalidEmail

let validateAge (request: SignupRequest) =
    request.Age
    |> Constraint.guard age
    |> Result.mapError InvalidAge
```

`guard` returns `Result<'value, Violation>`. Use `Constraint.check` when `Result<unit, Violation>` is enough, and
`Constraint.test` when a `bool` is.

Map the whole `Violation` into your own error case rather than rendering it here. It is comparable diagnostic data
carrying the failing atom, so it survives to the boundary that decides how — and in which language — to say it.

## Compose dependent results

```fsharp
let validateSignup (request: SignupRequest) =
    result {
        do! request.AcceptedTerms |> Result.requireTrue TermsNotAccepted
        let! name = validateName request
        let! email = validateEmail request
        let! age = validateAge request
        return { Name = name; Email = email; Age = age }
    }
```

`result { }` stops at the first application error. `AcceptedTerms` is a bare `bool` with no subject value to
preserve, which is what `Result.requireTrue` is for.

The `request` annotations are load-bearing: `Signup` is declared after `SignupRequest` and shares its field names, so
F# would otherwise infer the wrong record from `request.Name`.

Use Schema when independent fields should accumulate path-aware sibling diagnostics instead of stopping at the first.

## Render at the edge

```fsharp
let describe (renderer: Renderer) error =
    let field name violation =
        violation |> Violation.fullMessage (renderer |> Renderer.attribute name)

    match error with
    | TermsNotAccepted -> "The terms must be accepted."
    | InvalidName violation -> field "name" violation
    | InvalidEmail violation -> field "email" violation
    | InvalidAge violation -> field "age" violation
```

See [Using constraints](../constraint/overview/), [Working with violations](../constraint/violations/),
[Localization](../constraint/localization/), and [Result Builder](../result-builder/).
