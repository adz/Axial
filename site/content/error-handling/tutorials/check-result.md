---
weight: 10
title: Check and Result Tutorial
description: Validate a signup request end to end with Predicate, Check, and Result.
type: docs
---


This tutorial validates a signup request using all three pieces together: `Predicate` for a cheap local guard,
`Check` for the reusable named constraints on each field, and `Result` for the one-off condition and the domain
error that ties it all together.

## The Shape Of The Problem

The raw request is untyped strings and a bool — exactly what arrives from a form post. The goal is a
`Signup` record that can only exist if every field already passed its constraint.

```fsharp
open Axial
open Axial.ErrorHandling.CheckDSL

type SignupRequest =
    { Name: string
      Email: string
      Age: int
      Password: string
      ConfirmPassword: string
      AcceptedTerms: bool }

type SignupError =
    | EmptyRequest
    | TermsNotAccepted
    | NameInvalid
    | EmailInvalid
    | AgeInvalid
    | PasswordMismatch

type Signup =
    { Name: string
      Email: string
      Age: int }
```

## Predicate: A Cheap Guard Before The Real Work

Before running any `Check`, bail out on the obviously-empty request — someone submitted the form with nothing in
it. This is a one-off `bool` condition consumed immediately by an `if`, never carried anywhere as a `Result`, so it
belongs to `Predicate`/`PredicateExtensions`, not `Check`:

```fsharp
let private isBlankRequest (request: SignupRequest) =
    request.Name.IsBlank && request.Email.IsBlank
```

`IsBlank` is a `PredicateExtensions` member on `string` — true for null, empty, or whitespace-only. Nothing here
needs a structured failure; it's a guard clause, not a validation rule.

## Check: Named Constraints Per Field

Each field constraint is a reusable fact, so it's a `Check`. Open `Axial.ErrorHandling.CheckDSL` inside the module
that runs them and combine the ones that apply with `Check.all`:

```fsharp
let private nameCheck : Check<string> = Check.all [ present; lengthBetween 2 40 ]
let private emailCheck : Check<string> = Check.all [ present; email ]
let private ageCheck : Check<int> = atLeast 13
```

Attach the domain error at the boundary, once per field:

```fsharp
let private validateName request : Result<string, SignupError> =
    request.Name |> nameCheck |> orError NameInvalid

let private validateEmail request : Result<string, SignupError> =
    request.Email |> emailCheck |> orError EmailInvalid

let private validateAge request : Result<int, SignupError> =
    request.Age |> ageCheck |> orError AgeInvalid
```

`orError` discards the `CheckFailure list` and replaces it with the domain error. That fits here
because `SignupError` doesn't need to describe *which* constraint failed, only that the field did.

## Result: The Condition That Isn't A Check

Password confirmation isn't a reusable, named fact about one value — it's a comparison between two fields, bespoke
to this call site. That's `Result.requireTrue`, not `Check`:

```fsharp
let private validatePasswords request : Result<unit, SignupError> =
    (request.Password = request.ConfirmPassword)
    |> Result.requireTrue PasswordMismatch
```

The terms checkbox is the same shape — a bare `bool` already sitting on the request, not something to check the
structure of:

```fsharp
let private validateTerms request : Result<unit, SignupError> =
    request.AcceptedTerms |> Result.requireTrue TermsNotAccepted
```

## Compose With `result {}`

Every piece above is an independent `Result`. `result {}` sequences them fail-fast — the first failure stops the
workflow, and later steps never run against a request that already failed:

`let!` binds the value inside `Ok` to the name on its left. `do!` binds a `Result<unit, _>` step.
`return!` would use another `Result` as the result of the whole block.

```fsharp
result {
    do! (validateTerms request: Result<unit, SignupError>)
    let! (name: string) =
        (validateName request: Result<string, SignupError>)

    return { Name = name; Email = email; Age = age }
}
// Result<Signup, SignupError>
```

```fsharp
let validateSignup (request: SignupRequest) : Result<Signup, SignupError> =
    if isBlankRequest request then
        Error EmptyRequest
    else
        result {
            do! validateTerms request
            let! name = validateName request
            let! email = validateEmail request
            let! age = validateAge request
            do! validatePasswords request

            return { Name = name; Email = email; Age = age }
        }
```

```fsharp
validateSignup
    { Name = "Ada"
      Email = "ada@example.com"
      Age = 12
      Password = "hunter2"
      ConfirmPassword = "hunter2"
      AcceptedTerms = true }
// Error AgeInvalid — stops here; validatePasswords never runs
```

This is still ordinary pure code — no runtime, environment, cancellation token, task, or service provider. It can
be unit-tested by calling `validateSignup` directly.

## Where Each Piece Earned Its Place

- **`Predicate`** (`isBlankRequest`) — a `bool` consumed immediately by an `if`, never became a `Result`.
- **`Check`** (`nameCheck`, `emailCheck`, `ageCheck`) — reusable, named constraints, combined with `Check.all`,
  attached to `SignupError` with `orError`.
- **`Result`** (`validatePasswords`, `validateTerms`) — conditions bespoke to this workflow, via
  `Result.requireTrue`, composed with the `Check`-backed steps in one `result {}`.

See [Checks](../checks/) and [Predicates](../predicates/) for the full surface each one exposes, and
[Result Builder](../result-builder/) for more on `result {}`.

When independent fields should report all sibling failures together instead of stopping at the first one, move to
the [Validation tutorial]({{< relref "/error-handling/diagnostics/tutorials/registration-form.md" >}}).
