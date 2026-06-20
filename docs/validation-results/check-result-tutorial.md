---
weight: 15
title: Check, Result, and Validation Tutorial
description: Build pure validation with Check first, then attach typed errors, compose with result {}, and accumulate with validate {}.
---

# Check, Result, and Validation Tutorial

This page shows how to build pure validation without introducing `Flow`.

The example starts with facts about values, then turns those facts into typed `Result` values, then composes them with `result {}`. It finishes by showing when to switch to `validate {}` so sibling failures can accumulate.

## Start With Checks

`Check` helpers answer validation questions before you choose a domain error.

```fsharp
open FsFlow

Check.notBlank "Ada"          // Ok ()
Check.whenNotBlank "Ada"      // Ok "Ada"
Check.takeSome (Some "Ada")   // Ok "Ada"
```

Use the helper shape that matches the success value you need:

| Need | Shape | Example |
| --- | --- | --- |
| Only prove a fact | `Check.x` | `name |> Check.notBlank` |
| Keep the original input | `Check.whenX` | `name |> Check.whenNotBlank` |
| Extract an inner value | `Check.takeX` | `maybeUser |> Check.takeSome` |

These simple checks fail with `unit`. That means the check failed, but no application error has been chosen yet.

## Start From The Core Result Shape

The pure validation stack stays on the standard F# `Result<'value, 'error>` type. `Check` is just a small layer over that shape.

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

Once you have a result, use `Result.map`, `Result.bind`, and `Result.mapError` to keep the logic explicit. That keeps the code easy to test without any runtime, environment, or flow machinery.

## Attach Domain Errors

Use `Check.withError` when a unit-error check should become a domain result.

```fsharp
type RegistrationError =
    | NameMissing
    | EmailMissing
    | PrimaryIdInvalid of CardinalityFailure

let validateName name : Result<string, RegistrationError> =
    name
    |> Check.whenNotBlank
    |> Check.withError NameMissing

let validateEmail email : Result<string, RegistrationError> =
    email
    |> Check.whenNotBlank
    |> Check.withError EmailMissing
```

Some helpers already carry useful diagnostics. Keep those diagnostics until you map them deliberately.

```fsharp
let primaryId ids : Result<int, RegistrationError> =
    ids
    |> Check.takeSingle
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

## Accumulate Sibling Failures

Use `validate {}` when independent fields should all be reported together.

```fsharp
let validateRegistrationFields name email =
    validate {
        let! validName = validateName name
        and! validEmail = validateEmail email

        return validName, validEmail
    }
```

`validate {}` is the accumulating step in the stack. It is still pure, but it uses `Validation` and `Diagnostics` so sibling failures can be returned together.

## Use The Smallest Honest Shape

Choose the smallest shape that matches the problem:

- `Check` when you are still proving a fact
- `Result` when one failure should stop the workflow
- `Validation` when sibling failures should be accumulated

That keeps the validation code independent from `Flow` and makes it easy to move the same logic into a boundary later if needed.
