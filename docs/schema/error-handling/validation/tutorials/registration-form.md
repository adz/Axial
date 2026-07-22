---
weight: 10
title: Registration Form Tutorial
description: Accumulate independent field failures with validate {} and Diagnostics.
---

# Registration Form Tutorial

This tutorial validates independent fields and returns every sibling failure.

Use `Validation` when the user can fix several fields at once. Use `Result` when each step depends on the previous successful value.

## Define Field Errors

```fsharp
open Axial
open Axial.ErrorHandling.CheckDSL

type RegistrationError =
    | NameMissing
    | EmailMissing
```

## Reuse Result Checks

Use `Check` to build typed field results, then let `validate {}` lift them into `Validation`.

```fsharp
let validateName name : Result<string, RegistrationError> =
    name
    |> present
    |> orError NameMissing

let validateEmail email : Result<string, RegistrationError> =
    email
    |> present
    |> orError EmailMissing
```

## Accumulate With validate

Use `and!` for independent fields. Both checks run, and both errors can be returned.

`let!` and `and!` bind the successful strings to the names on their left. The right-hand results keep their error
type, and the whole block returns a `Validation`:

```fsharp
validate {
    let! (validName: string) =
        (validateName name: Result<string, RegistrationError>)

    and! (validEmail: string) =
        (validateEmail email: Result<string, RegistrationError>)

    return { Name = validName; Email = validEmail }
}
// Validation<Registration, RegistrationError>
```

```fsharp
type Registration =
    { Name: string
      Email: string }

let validateRegistration name email : Validation<Registration, RegistrationError> =
    validate {
        let! validName = validateName name
        and! validEmail = validateEmail email

        return
            { Name = validName
              Email = validEmail }
    }
```

## Convert At Boundaries

Stay in `Validation` while collecting field errors. Convert only when another API expects ordinary `Result`.

```fsharp
let asResult name email : Result<Registration, Diagnostics<RegistrationError>> =
    validateRegistration name email
    |> Validation.toResult
```

Move to [Diagnostics](../../diagnostics/) when you need paths, indexes, names, or custom rendering.
