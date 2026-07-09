---
weight: 10
title: Check and Result Tutorial
description: Build typed fail-fast results from pure Check helpers.
---

# Check and Result Tutorial

This tutorial builds pure fail-fast validation without introducing `Flow`.

The example starts with facts about values, turns those facts into typed `Result` values, and composes them with `result {}`.

## Start With Checks

`Check` helpers answer validation questions before you choose a domain error.

```fsharp
open Axial

Check.present "Ada"              // Ok "Ada"
Result.someOr "missing" (Some "Ada")    // Ok "Ada"
```

Use the helper shape that matches the success value you need:

| Need | Shape | Example |
| --- | --- | --- |
| A reusable, named check (keeps the input) | `Check.x` | `name |> Check.present` |
| Extract an inner value | `Result.x` | `maybeUser |> Result.someOr MissingUser` |

`Check` calls fail with `CheckFailure list`, not `unit` — the check failed for a structured, describable reason, but
no application error has been chosen yet.

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

`Check` already keeps the input on success, so attaching a domain error is a single `Result.orError` away.

```fsharp
open Axial.Refined

type RegistrationError =
    | NameMissing
    | EmailMissing
    | PrimaryIdInvalid of RefinementError

let validateName name : Result<string, RegistrationError> =
    name
    |> Check.present
    |> Result.orError NameMissing

let validateEmail email : Result<string, RegistrationError> =
    email
    |> Check.present
    |> Result.orError EmailMissing
```

Some helpers already carry useful diagnostics. Keep those diagnostics until you map them deliberately.
`Refine.exactlyOne`/`Refine.atMostOne` extract a single element from a sequence — cardinality is a collection-level
structural fact, not a value-level `Check`, so it lives in `Refine` alongside the other structural refinements.

```fsharp
let primaryId ids : Result<int, RegistrationError> =
    ids
    |> Refine.exactlyOne
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

- `Check` for a reusable, named constraint — it already keeps the input value on success
- `Result` for one-off conditions, extraction, and stopping the workflow at the first failure

That keeps validation code independent from `Flow`, so it can move into an application boundary later.

When independent fields should report all sibling failures together, move to the [Validation tutorial]({{< relref "/schema/validation/tutorials/registration-form.md" >}}).
