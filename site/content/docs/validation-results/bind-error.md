---
weight: 25
title: BindError
type: docs
description: Flow bind-site error assignment and mapping.
---


This page shows how to assign or map a source error immediately before `flow {}` binds that source.

`flow {}` can bind many source shapes directly. When the source already has the right error type, bind it directly. Use `BindError` only when the bind source needs a different error at the call site.

## Assign an Error

Use `BindError.withError` when the source fails with missingness, falsehood, or `unit`.

```fsharp
type User = { Name: string }
type LoginError = UserNotFound | InvalidPassword

let tryGetUser username : Async<User option> =
    async { return if username = "ada" then Some { Name = username } else None }

let login username password =
    flow {
        let! user =
            tryGetUser username
            |> BindError.withError UserNotFound

        do!
            password
            |> Check.notBlank
            |> BindError.withError InvalidPassword

        return user
    }
```

`BindError.withError` works on:

- `bool`
- `Option<'value>`
- `ValueOption<'value>`
- `Result<'value, unit>`
- `Flow<'env, unit, 'value>`
- `Async<bool>`, `Task<bool>`, `ValueTask<bool>`
- `Async<Option<'value>>`, `Task<Option<'value>>`, `ValueTask<Option<'value>>`
- `Async<ValueOption<'value>>`, `Task<ValueOption<'value>>`, `ValueTask<ValueOption<'value>>`
- `Async<Result<'value, unit>>`, `Task<Result<'value, unit>>`, `ValueTask<Result<'value, unit>>`

## Map an Error

Use `BindError.map` when the source already carries a meaningful error, but it is not the error type of the surrounding flow.

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
            |> BindError.map Unauthorized

        return!
            createToken user
            |> BindError.map TokenFailed
    }
```

`BindError.map` works on:

- `Result<'value, 'error>`
- `Flow<'env, 'error, 'value>`
- `Async<Result<'value, 'error>>`
- `Task<Result<'value, 'error>>`
- `ValueTask<Result<'value, 'error>>`

## When Not To Use It

Do not use `BindError` as a general Result helper. In pure code, use `Check.withError`, `Result.mapError`, or `Validation.mapError`.

```fsharp
let validateName name =
    name
    |> Take.whenNotBlank
    |> Check.withError "Name required"
```

Inside `flow {}`, direct binding is still the default. Reach for `BindError` only when the source error must be assigned or mapped before the bind.
