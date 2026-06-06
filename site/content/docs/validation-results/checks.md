---
weight: 10
title: Check and Take
description: Choose between yes/no checks and value-returning checks before entering Result, Validation, or Flow.
type: docs
---


This page shows how to choose between `Check` and `Take`, the pure validation helpers that sit before `Result`, `validate {}`, and `flow {}`.

Start by deciding what success should carry.

| Intent | Helper shape | Success value |
| --- | --- | --- |
| Require a fact and keep no value | `value |> Check.x` | `unit` |
| Require a fact and keep the original input | `value |> Take.whenX` | the original input |
| Extract or narrow the useful value | `value |> Take.x` | the extracted value |

`Check` answers a yes/no question. `Take` answers the same kind of question when the next step needs a value.

## Attach an Error

Most `Check` helpers and the option, nullable, string, and collection-preserving `Take` helpers return `Result<'value, unit>`. The `unit` failure means "this check failed, but no domain error has been chosen yet".

Use `Check.withError` to assign the application error in pure code when the source has a `unit` error.

```fsharp
type SignUpError =
    | NameRequired
    | UserMissing
    | AgeInvalid

type User = { Name: string }

let requireName name : Result<string, SignUpError> =
    name
    |> Take.whenNotBlank
    |> Check.withError NameRequired

let requireUser maybeUser : Result<User, SignUpError> =
    maybeUser
    |> Take.some
    |> Check.withError UserMissing

let requireAdult age : Result<unit, SignUpError> =
    age >= 18
    |> Check.isTrue
    |> Check.withError AgeInvalid
```

The same function attaches errors to both `Check` and `Take` because both are unit-error checks.

## Choose Check

Use `Check` when success is only a gate.

```fsharp
type RegistrationError =
    | NameRequired
    | PasswordRequired

let validatePassword password : Result<unit, RegistrationError> =
    password
    |> Check.notBlank
    |> Check.withError PasswordRequired
```

This is useful for `do!` in `result {}` or `validate {}` blocks because the workflow does not need a value from the check.

```fsharp
let register name password =
    result {
        let! validName = name |> Take.whenNotBlank |> Check.withError NameRequired
        do! password |> Check.notBlank |> Check.withError PasswordRequired
        return validName
    }
```

## Choose Take.whenX

Use `Take.whenX` when the predicate should return the original input.

```fsharp
type EmailError =
    | EmailRequired

let validateEmail email : Result<string, EmailError> =
    email
    |> Take.whenNotBlank
    |> Check.withError EmailRequired
```

`Take.whenNotBlank` checks that the string is not blank and returns the original string. That makes it the right choice for `let! validEmail = ...`.

## Choose Take.x

Use bare `Take.x` helpers when the predicate exposes a narrower value.

```fsharp
type LookupError =
    | UserMissing

type User = { Name: string }

let requireExistingUser maybeUser : Result<User, LookupError> =
    maybeUser
    |> Take.some
    |> Check.withError UserMissing
```

`Take.some` checks that the option is `Some` and returns the unwrapped value.

## Cardinality

Cardinality has all three success shapes.

```fsharp
ids |> Check.exactlyOne
ids |> Take.whenExactlyOne
ids |> Take.exactlyOne
```

Use `Check.exactlyOne` when you only need to know the fact, `Take.whenExactlyOne` when the next step needs the original collection, and `Take.exactlyOne` when the next step needs the single element.

The preserving cardinality helpers enumerate up to two items before returning the original collection. Prefer arrays, lists, or other reusable collections for `Take.whenExactlyOne` and `Take.whenAtMostOne`; use the extracting helpers when the next step only needs the element.

The `Take` cardinality helpers already carry `CardinalityFailure` because the count is useful diagnostic information. Use `Result.mapError` when the caller needs a domain error.

```fsharp
type OrderId = OrderId of int
type OrderError = InvalidPrimaryId of CardinalityFailure

let primaryId ids : Result<OrderId, OrderError> =
    ids
    |> Take.exactlyOne
    |> Result.mapError InvalidPrimaryId
```

## Flow Bind Sites

Inside `flow {}`, use `BindError.withError` when a source needs an error assigned immediately before binding.

```fsharp
type LoginError =
    | MissingPassword

let login password =
    flow {
        do!
            password
            |> Check.notBlank
            |> BindError.withError MissingPassword

        return ()
    }
```

Outside `flow {}`, keep pure code in `Result` with `Check.withError`. When a source already carries a meaningful error, use `Result.mapError`, `Validation.mapError`, or `BindError.map` instead.
