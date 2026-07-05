---
weight: 10
title: Check
description: Use structured Check programs before shaping failures with Result, Validation, or Flow.
aliases:
  - /docs/validation-results/checks/
type: docs
---


`Check` contains reusable value constraints. Executable checks such as `Check.String.present` return
`Result<unit, CheckFailure list>`. Top-level `Check.*` helpers are structured checks; use ordinary F# predicates when a
raw boolean is enough.

Use `Check` when success only proves a fact. Use `Result` when success should preserve, extract, or reshape a value.

| Intent | Use |
| --- | --- |
| Prove a fact and keep no value | `Check.String.present value` |
| Preserve the input on success | `Result.notBlank value` |
| Extract an inner value | `Result.someOr error value` |
| Attach a domain error to a check | `Result.require check value |> Result.mapError mapper` |
| Attach a domain error to a boolean | `Result.checkOr error condition` |

## Attach an Error

`Check` failures are reusable structural facts. Map them to domain errors at the boundary where the domain decision is
known.

Use `Result.require` when an executable check should become a unit-success result.

```fsharp
type SignUpError =
    | NameRequired
    | UserMissing
    | AgeInvalid

type User = { Name: string }

let requireAdult age : Result<unit, SignUpError> =
    age
    |> Result.require (Check.Number.atLeast 18)
    |> Result.mapError (fun _ -> AgeInvalid)
```

Use the value-preserving and extracting helpers in `Result` when success should carry a value.

```fsharp
let requireName name : Result<string, SignUpError> =
    Result.notBlank name |> Result.mapError (fun _ -> NameRequired)

let requireUser maybeUser : Result<User, SignUpError> =
    maybeUser |> Result.someOr UserMissing
```

Some helpers already return a useful diagnostic error. Use `Result.mapError` for those.

```fsharp
type OrderError =
    | InvalidPrimaryId of CardinalityFailure
    | InvalidQuantity of CheckFailure list

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

Use `Result.checkOr` when a raw boolean should become a result.

```fsharp
type RegistrationError =
    | PasswordRequired

let validatePassword password : Result<unit, RegistrationError> =
    not (System.String.IsNullOrWhiteSpace password)
    |> Result.checkOr PasswordRequired
```

Use `Result.require` when the gate is a reusable `Check<'value>`.

```fsharp
type RegistrationError =
    | NameRequired
    | PasswordRequired

let register name password =
    result {
        let! validName = Result.notBlank name |> Result.mapError (fun _ -> NameRequired)
        do!
            password
            |> Result.require Check.String.present
            |> Result.mapError (fun _ -> PasswordRequired)

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
    |> Result.mapError (fun _ -> EmailRequired)
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
    |> Result.someOr UserMissing
```

`Result.someOr` checks that the option is `Some` and returns the unwrapped value.

## Common Families

| Predicate | Preserve or extract |
| --- | --- |
| `Check.Option.some` | `Result.someOr` |
| `Check.ValueOption.some` | `Result.valueSomeOr` |
| `Check.Nullable.hasValue` | `Result.nullableOr` |
| ordinary non-null predicate | `Result.notNullOr` |
| `Check.Result.ok` | `Result.okOr` |
| `Check.Result.error` | `Result.errorOr` |
| `Check.Seq.notEmpty` | `Result.atLeastOne` or `Result.headOr` |
| `Check.single` | `Result.single` |
| `Check.atMostOne` | `Result.atMostOne` |
| `Check.String.present` | `Result.notBlank` |
| `Check.positive` | `Result.greaterThan 0` |

## Cardinality

Cardinality helpers keep `CardinalityFailure` because the count is useful diagnostic information.

```fsharp
ids |> Check.single
ids |> Result.single
ids |> Result.atMostOne
```

Use `Check.single` when you only need to know the fact. Use `Result.single` when the next step needs the single element.

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
            |> Result.mapError (fun _ -> MissingPassword)

        return ()
    }
```
