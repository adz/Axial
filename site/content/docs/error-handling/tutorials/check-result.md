---
weight: 10
title: Check and Result Tutorial
description: Build typed fail-fast results from pure Check helpers.
aliases:
  - /docs/result/tutorial/
  - /docs/validation-results/check-result-tutorial/
type: docs
---


This tutorial builds pure fail-fast validation without introducing `Flow`.

The example starts with facts about values, turns those facts into typed `Result` values, and composes them with `result {}`.

## Start With Checks

`Check` helpers answer validation questions before you choose a domain error.

```fsharp
open Axial

Check.String.present "Ada"              // Ok ()
Result.notBlank "Ada"                   // Ok "Ada"
Result.someOr "missing" (Some "Ada")    // Ok "Ada"
```

Use the helper shape that matches the success value you need:

| Need | Shape | Example |
| --- | --- | --- |
| Only prove a fact | `Check.x` | `name |> Check.String.present` |
| Keep the original input | `Result.x` | `name |> Result.notBlank` |
| Extract an inner value | `Result.x` | `maybeUser |> Result.someOr MissingUser` |

These simple checks fail with `unit`. That means the check failed, but no application error has been chosen yet.

## Start From The Core Result Shape

The pure validation stack stays on the standard F# `Result<'value, 'error>` type. `Check` is just the predicate layer over that shape.

```fsharp
let parsed =
    "41"
    |> System.Int32.TryParse
    |> function
        | true, value -> Ok value
        | false, _ -> Error "not an int"

let mapped =
    parsed
    |> Result.map ((+) 1)
```

Once you have a result, use `Result.map`, `Result.bind`, and `Result.mapError` to keep the logic explicit. The code stays testable without a runtime, environment, or flow machinery.

## Attach Domain Errors

Use value-preserving `Result` helpers when success should carry the input.

```fsharp
type RegistrationError =
    | NameMissing
    | EmailMissing
    | PrimaryIdInvalid of CardinalityFailure

let validateName name : Result<string, RegistrationError> =
    name
    |> Result.notBlank
    |> Result.mapError (fun _ -> NameMissing)

let validateEmail email : Result<string, RegistrationError> =
    email
    |> Result.notBlank
    |> Result.mapError (fun _ -> EmailMissing)
```

Some helpers already carry useful diagnostics. Keep those diagnostics until you map them deliberately.

```fsharp
let primaryId ids : Result<int, RegistrationError> =
    ids
    |> Result.single
    |> Result.mapError PrimaryIdInvalid
```

## Compose With Result

Use `result {}` when later steps depend on earlier successful values and the first failure should stop the workflow.

```fsharp
type Registration =
    { Name: string
      Email: string
      PrimaryId: int }

let validateRegistration name email ids : Result<Registration, RegistrationError> =
    result {
        let! validName = validateName name
        let! validEmail = validateEmail email
        let! validPrimaryId = primaryId ids

        return
            { Name = validName
              Email = validEmail
              PrimaryId = validPrimaryId }
    }
```

This is still ordinary pure code. It can be unit-tested without a runtime, environment, cancellation token, task, or service provider.

## Use The Smallest Honest Shape

Choose the smallest shape that matches the problem:

- `Check` when you are still proving a fact
- `Result` when one failure should stop the workflow

That keeps validation code independent from `Flow`, so it can move into an application boundary later.

When independent fields should report all sibling failures together, move to the [Validation tutorial](../../../validation/tutorials/registration-form/).
