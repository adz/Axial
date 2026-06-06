---
weight: 10
title: Check
description: Choose between predicate, preserving, and extracting Check helpers before entering Result, Validation, or Flow.
---

# Check

This page shows how to choose between the three `Check` helper shapes.

Start by deciding what success should carry.

| Intent | Helper shape | Success value |
| --- | --- | --- |
| Require a fact and keep no value | `value |> Check.x` | `unit` |
| Require a fact and keep the original input | `value |> Check.whenX` | the original input |
| Extract or narrow the useful value | `value |> Check.takeX` | the extracted value |

Unprefixed helpers are predicates. `when*` helpers are value-preserving gates. `take*` helpers unwrap or narrow a structure.

## Attach an Error

Most simple helpers return `Result<'value, unit>`. The `unit` failure means "this check failed, but no domain error has been chosen yet".

Use `Check.withError` to assign the application error in pure code.

```fsharp
type SignUpError =
    | NameRequired
    | UserMissing
    | AgeInvalid

type User = { Name: string }

let requireName name : Result<string, SignUpError> =
    name
    |> Check.whenNotBlank
    |> Check.withError NameRequired

let requireUser maybeUser : Result<User, SignUpError> =
    maybeUser
    |> Check.takeSome
    |> Check.withError UserMissing

let requireAdult age : Result<unit, SignUpError> =
    age >= 18
    |> Check.isTrue
    |> Check.withError AgeInvalid
```

Some helpers already return a useful diagnostic error. Use `Result.mapError` for those.

```fsharp
type OrderError =
    | InvalidPrimaryId of CardinalityFailure
    | InvalidQuantity of RangeFailure<int>

let primaryId ids : Result<int, OrderError> =
    ids
    |> Check.takeSingle
    |> Result.mapError InvalidPrimaryId

let quantity value : Result<int, OrderError> =
    value
    |> Check.whenPositive
    |> Result.mapError InvalidQuantity
```

## Choose a Predicate

Use an unprefixed helper when success is only a gate.

```fsharp
type RegistrationError =
    | PasswordRequired

let validatePassword password : Result<unit, RegistrationError> =
    password
    |> Check.notBlank
    |> Check.withError PasswordRequired
```

This is useful for `do!` in `result {}` or `validate {}` blocks because the workflow does not need a value from the check.

```fsharp
type RegistrationError =
    | NameRequired
    | PasswordRequired

let register name password =
    result {
        let! validName = name |> Check.whenNotBlank |> Check.withError NameRequired
        do! password |> Check.notBlank |> Check.withError PasswordRequired
        return validName
    }
```

## Choose `when*`

Use `Check.whenX` when the predicate should return the original input.

```fsharp
type EmailError =
    | EmailRequired

let validateEmail email : Result<string, EmailError> =
    email
    |> Check.whenNotBlank
    |> Check.withError EmailRequired
```

`Check.whenNotBlank` checks that the string is not blank and returns the original string. That makes it the right choice for `let! validEmail = ...`.

## Choose `take*`

Use `Check.takeX` helpers when the predicate exposes a narrower value.

```fsharp
type LookupError =
    | UserMissing

type User = { Name: string }

let requireExistingUser maybeUser : Result<User, LookupError> =
    maybeUser
    |> Check.takeSome
    |> Check.withError UserMissing
```

`Check.takeSome` checks that the option is `Some` and returns the unwrapped value.

## Common Families

| Predicate | Preserve original | Extract/narrow |
| --- | --- | --- |
| `Check.isSome` | `Check.whenSome` | `Check.takeSome` |
| `Check.isValueSome` | `Check.whenValueSome` | `Check.takeValueSome` |
| `Check.hasValue` | `Check.whenHasValue` | `Check.takeHasValue` |
| `Check.notNull` | `Check.whenNotNull` | `Check.takeNotNull` |
| `Check.isOk` | `Check.whenOk` | `Check.takeOk` |
| `Check.isError` | `Check.whenError` | `Check.takeError` |
| `Check.notEmpty` | `Check.whenNotEmpty` | `Check.takeHead` |
| `Check.isSingle` | `Check.whenSingle` | `Check.takeSingle` |
| `Check.atMostOne` | `Check.whenAtMostOne` | `Check.takeAtMostOne` |
| `Check.notBlank` | `Check.whenNotBlank` | none |
| `Check.positive` | `Check.whenPositive` | none |

## Cardinality

Cardinality helpers keep `CardinalityFailure` because the count is useful diagnostic information.

```fsharp
ids |> Check.isSingle
ids |> Check.whenSingle
ids |> Check.takeSingle
```

Use `Check.isSingle` when you only need to know the fact, `Check.whenSingle` when the next step needs the original collection, and `Check.takeSingle` when the next step needs the single element.

The preserving cardinality helpers enumerate enough items to establish the cardinality before returning the original collection. Prefer arrays, lists, or other reusable collections for `Check.whenSingle` and `Check.whenAtMostOne`; use the extracting helpers when the next step only needs the element.

## Flow Bind Sites

Outside `flow {}`, keep pure code in `Result` with `Check.withError` or `Result.mapError`.

Inside `flow {}`, use `BindError.withError` only when a source needs an error assigned immediately before binding.

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
