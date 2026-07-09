---
weight: 25
title: Bind
type: docs
description: Flow bind-site error assignment and mapping.
---


Use `Bind` to assign or map a source error immediately before `flow {}` binds that source.

`flow {}` can bind many source shapes directly. When the source already has the right error type, bind it directly. Use `Bind` only when the bind source needs a different error at the call site.

## Assign an Error

Use `Bind.error` when the source fails with option/value-option absence or a `unit` error.

```fsharp
type User = { Name: string }
type LoginError = UserNotFound | InvalidPassword

let tryGetUser username : Async<User option> =
    async { return if username = "ada" then Some { Name = username } else None }

let login username password =
    flow {
        let! user =
            tryGetUser username
            |> Bind.error UserNotFound

        do!
            password
            |> Check.present
            |> Result.orError InvalidPassword
            |> Result.map ignore

        return user
    }
```

`Bind.error` works on:

- `Option<'value>`
- `ValueOption<'value>`
- `Result<'value, unit>`
- `Flow<'env, unit, 'value>`
- `Async<Option<'value>>`, `Task<Option<'value>>`, `ValueTask<Option<'value>>`
- `Async<ValueOption<'value>>`, `Task<ValueOption<'value>>`, `ValueTask<ValueOption<'value>>`
- `Async<Result<'value, unit>>`, `Task<Result<'value, unit>>`, `ValueTask<Result<'value, unit>>`

For boolean predicates, make the predicate explicit first:

```fsharp
do!
    Result.requireTrue InvalidPassword isValid
```

## Map an Error

Use `Bind.mapError` when the source already carries a meaningful error, but it is not the error type of the surrounding flow.

```fsharp
type AuthError = Denied of string
type TokenError = Expired of string
type LoginError = Unauthorized of AuthError | TokenFailed of TokenError

let authorize user : Async<Result<unit, AuthError>> =
    async { return Error (Denied user) }

let createToken user : Result<string, TokenError> =
    Error (Expired user)

let login user =
    flow {
        do!
            authorize user
            |> Bind.mapError Unauthorized

        return!
            createToken user
            |> Bind.mapError TokenFailed
    }
```

`Bind.mapError` works on:

- `Result<'value, 'error>`
- `Flow<'env, 'error, 'value>`
- `Async<Result<'value, 'error>>`
- `Task<Result<'value, 'error>>`
- `ValueTask<Result<'value, 'error>>`

## Bind Versus Policy

`Bind.error`, `Bind.mapError`, and `Policy` all assign workflow errors, but at different altitudes:

- use `Bind.error` when a single bind source fails without a meaningful error — option absence or a `unit` error — and
  the call site knows which workflow error that absence means
- use `Bind.mapError` when a single bind source already carries a meaningful error that just needs translating into the
  surrounding flow's error type
- use `Policy` when the requirement is a reusable, named verification step — especially when it should read the
  workflow environment, compose with other requirements, or switch on and off per environment

A `Policy<'env, 'error, 'input, 'output>` is a value you define once and run anywhere with `Flow.verify`:

```fsharp
let withinLimit =
    Policy.context
        (fun env value -> if value <= env.Limit then Ok value else Error ())
        (fun () -> TooLarge)

let workflow count =
    flow {
        let! checkedCount = count |> Flow.verify (Policy.optional _.EnforceLimit withinLimit)
        return checkedCount
    }
```

If the adjustment is a one-off at a single bind site, stay with `Bind`. When the same requirement appears in more than
one workflow, needs the environment, or reads better with a name, promote it to a `Policy`.

## When Not To Use It

Do not use `Bind` as a general Result helper. In pure code, use a `Check<'value>` call directly, `Result.mapError`/`Result.orError`, or `Validation.mapError`.

```fsharp
let validateName name =
    name
    |> Check.present
    |> Result.orError "Name required"
```

Inside `flow {}`, direct binding is still the default. Reach for `Bind` only when the source error must be assigned or mapped before the bind.
