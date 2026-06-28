---
weight: 10
title: Check
description: Use pure Check predicates before shaping failures with Result, Validation, or Flow.
aliases:
  - /docs/validation-results/checks/
type: docs
---


`Check` contains pure predicates. A check answers a yes/no question and returns `bool`.

Use `Check` when success only proves a fact. Use `Result` when success should preserve, extract, or reshape a value.

| Intent | Use |
| --- | --- |
| Prove a fact and keep no value | `Check.notBlank value` |
| Preserve the input on success | `Result.notBlank value` |
| Extract an inner value | `Result.some value` |
| Attach a domain error to a predicate | `Result.require condition error` |

## Attach an Error

`Check` predicates do not carry errors. They only decide whether a local fact is true.

Use `Result.require` when a boolean condition should become a domain result.

```fsharp
type SignUpError =
    | NameRequired
    | UserMissing
    | AgeInvalid

type User = { Name: string }

let requireAdult age : Result<unit, SignUpError> =
    Result.require (age >= 18) AgeInvalid
```

Use the value-preserving and extracting helpers in `Result` when success should carry a value.

```fsharp
let requireName name : Result<string, SignUpError> =
    Result.notBlank name |> Result.mapError (fun () -> NameRequired)

let requireUser maybeUser : Result<User, SignUpError> =
    Result.some maybeUser |> Result.mapError (fun () -> UserMissing)
```

Some helpers already return a useful diagnostic error. Use `Result.mapError` for those.

```fsharp
type OrderError =
    | InvalidPrimaryId of CardinalityFailure
    | InvalidQuantity of RangeFailure<int>

let primaryId ids : Result<int, OrderError> =
    ids
    |> Result.single
    |> Result.mapError InvalidPrimaryId

let quantity value : Result<int, OrderError> =
    value
    |> Result.greaterThan 0
    |> Result.mapError InvalidQuantity
```

## Choose a Predicate

Use `Check` when success is only a gate.

```fsharp
type RegistrationError =
    | PasswordRequired

let validatePassword password : Result<unit, RegistrationError> =
    if Check.notBlank password |> Result.isOk then
        Ok ()
    else
        Error PasswordRequired
```

In most application code, `Result.require` is terser because it performs the same gate and assigns the error in one step.

```fsharp
type RegistrationError =
    | NameRequired
    | PasswordRequired

let register name password =
    result {
        let! validName = Result.notBlank name |> Result.mapError (fun () -> NameRequired)
        do! Result.require (Check.notBlank password |> Result.isOk) PasswordRequired
        return validName
    }
```

## Preserve the Input

```fsharp
type EmailError =
    | EmailRequired

let validateEmail email : Result<string, EmailError> =
    email
    |> Result.notBlank
    |> Result.mapError (fun () -> EmailRequired)
```

`Result.notBlank` checks that the string is not blank and returns the original string.

## Extract Values

Use `Result` helpers when success exposes an inner value or a deliberately different success shape.

```fsharp
type LookupError =
    | UserMissing

type User = { Name: string }

let requireExistingUser maybeUser : Result<User, LookupError> =
    maybeUser
    |> Result.some
    |> Result.mapError (fun () -> UserMissing)
```

`Result.some` checks that the option is `Some` and returns the unwrapped value.

## Common Families

| Predicate | Preserve or extract |
| --- | --- |
| `Check.isSome` | `Result.some` |
| `Check.isValueSome` | `Result.valueSome` |
| `Check.hasValue` | `Result.nullable` |
| `Check.notNull` | `Result.notNull` |
| `Check.isOk` | `Result.okValue` |
| `Check.isError` | `Result.errorValue` |
| `Check.notEmpty` | `Result.notEmpty` or `Result.head` |
| `Check.isSingle` | `Result.single` |
| `Check.atMostOne` | `Result.atMostOne` |
| `Check.notBlank` | `Result.notBlank` |
| `Check.positive` | `Result.greaterThan 0` |

## Cardinality

Cardinality helpers keep `CardinalityFailure` because the count is useful diagnostic information.

```fsharp
ids |> Check.isSingle
ids |> Result.single
ids |> Result.atMostOne
```

Use `Check.isSingle` when you only need to know the fact. Use `Result.single` when the next step needs the single element.

## Flow Bind Sites

Outside `flow {}`, keep pure code in `Result` with `Result.require`, value-preserving helpers, extracting helpers, or `Result.mapError`.

Inside `flow {}`, use `Bind.error` only when a source needs an error assigned immediately before binding.

```fsharp
type LoginError =
    | MissingPassword

let login password =
    flow {
        do!
            password
            |> Result.notBlank
            |> Result.mapError (fun () -> MissingPassword)

        return ()
    }
```
