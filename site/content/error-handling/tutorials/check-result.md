---
weight: 10
title: Check and Result Tutorial
description: Apply reusable checks and compose checked values through Result.guard.
type: docs
---


This tutorial checks a signup request and maps check failures into an application error.

```fsharp
open Axial.Check
open Axial.Result

type SignupRequest =
    { Name: string
      Email: string
      Age: int
      AcceptedTerms: bool }

type SignupError =
    | TermsNotAccepted
    | InvalidName of CheckFailure list
    | InvalidEmail of CheckFailure list
    | InvalidAge of CheckFailure list

type Signup =
    { Name: string
      Email: string
      Age: int }
```

## Define checks

```fsharp
let nameCheck : Check<string> =
    Check.all [ Check.String.present; Check.String.lengthBetween 2 40 ]

let emailCheck : Check<string> =
    Check.all [ Check.String.present; Check.String.email ]

let ageCheck : Check<int> =
    Check.atLeast 13
```

Each successful check returns `Ok ()`.

## Preserve checked values

```fsharp
let validateName request =
    request.Name
    |> Result.guard nameCheck
    |> Result.mapError InvalidName

let validateEmail request =
    request.Email
    |> Result.guard emailCheck
    |> Result.mapError InvalidEmail

let validateAge request =
    request.Age
    |> Result.guard ageCheck
    |> Result.mapError InvalidAge
```

## Compose dependent results

```fsharp
let validateSignup request =
    result {
        do! request.AcceptedTerms |> Result.requireTrue TermsNotAccepted
        let! name = validateName request
        let! email = validateEmail request
        let! age = validateAge request
        return { Name = name; Email = email; Age = age }
    }
```

`result { }` stops at the first application error. Use Schema when independent fields should accumulate path-aware
sibling diagnostics.

See [Using Check](../check/overview/) and [Result Builder](../result-builder/).
