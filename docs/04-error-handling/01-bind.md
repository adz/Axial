---
title: Bind
type: docs
description: Assign or map an error at a flow computation-expression bind site.
---

# Bind

Use `Bind.error` and `Bind.mapError` to give different bind sources the same error-assignment syntax.

## Why use Bind

Without `Bind`, you must adapt the right-hand side before `flow { }` can bind it. The required transformation depends
on that source's shape. For example, mapping an `Async<Result<_,_>>` error requires another `async { }` block:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let authorizeForLogin user =
    async {
        let! result = authorize user
        return result |> Result.mapError Unauthorized
    }

let login user =
    flow {
        do! authorizeForLogin user
        return user
    }
```

The equivalent transformation for a `Result` uses `Result.mapError` directly. A `Flow` uses `Flow.mapError`. Assigning
an error to `Option` or `Async<Option<_>>` requires another shape-specific conversion.

`Bind` gives these supported bind sources one bind-site syntax. You choose whether to assign or map the error; the
Flow computation expression handles the source shape:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
flow {
    let! profile = maybeProfile |> Bind.error ProfileNotFound
    do! authorize user |> Bind.mapError Unauthorized
    return! createToken user |> Bind.mapError TokenFailed
}
```

Here, `maybeProfile` can be an `Option` or an asynchronous option source. Similarly, error mapping has the same form
for `Result`, `Async<Result<_,_>>`, and `Flow`.

Use `Bind` directly with `let!`, `do!`, or `return!`. It marks the error adaptation for that bind and does not run the
source. If the source already uses the workflow's error type, bind it without `Bind`.

## Assign an error

`Bind.error failure source` assigns `failure` to a source that has no error value of its own. It preserves a present
or successful value. It turns `None`, `ValueNone`, or `Error ()` into the supplied workflow error when `flow { }`
binds the source.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
type User = { Name: string }
type LoginError = UserNotFound | InvalidPassword

let tryGetUser username : Async<User option> =
    async { return if username = "ada" then Some { Name = username } else None }

let checkPassword password =
    if System.String.IsNullOrWhiteSpace password then Error () else Ok ()

let login username password =
    flow {
        let! user =
            tryGetUser username
            |> Bind.error UserNotFound

        do!
            checkPassword password
            |> Bind.error InvalidPassword

        return user
    }
```

`Bind.error` accepts these source types:

- `Option<'value>`
- `ValueOption<'value>`
- `Result<'value, unit>`
- `Flow<'env, unit, 'value>`
- `Async<Option<'value>>`, `Task<Option<'value>>`, and `ValueTask<Option<'value>>`
- `Async<ValueOption<'value>>`, `Task<ValueOption<'value>>`, and `ValueTask<ValueOption<'value>>`
- `Async<Result<'value, unit>>`, `Task<Result<'value, unit>>`, and `ValueTask<Result<'value, unit>>`

For a Boolean condition, first return a `Result` that states what failure looks like. Then bind that result directly
if it already uses the workflow error type, or apply `Bind.error` if it uses `unit`.

## Map an error

Use `Bind.mapError` when the source has a meaningful error that must be translated to the surrounding workflow's
error type.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
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

`Bind.mapError` accepts these source types:

- `Result<'value, 'error>`
- `Flow<'env, 'error, 'value>`
- `Async<Result<'value, 'error>>`
- `Task<Result<'value, 'error>>`
- `ValueTask<Result<'value, 'error>>`

## Use a marker only at a bind site

The value returned by `Bind.error` or `Bind.mapError` is a marker for the Flow computation expression. It is not a
general-purpose `Result` or Flow transformation. Keep it directly on the right side of `let!`, `do!`, or `return!`:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
flow {
    let! user = maybeUser |> Bind.error UserNotFound
    return! createToken user |> Bind.mapError TokenFailed
}
```

Outside `flow { }`, use functions for the source type, such as `Result.mapError`, `Option.toResult`, or
`Flow.mapError`.

Raw `Task` and `ValueTask` values still require an explicit
[Task interop adapter](/the-flow-type/task-async-interop.html). `Bind` changes an error at a computation-expression
bind; it does not change when an operation starts.

## Choose between Bind and Policy

Use `Bind` for a one-time error adaptation at a specific bind site. Use a
[Policy](/error-handling/policy.html) for a named, reusable verification rule that can read the workflow environment,
compose with other rules, or be enabled by the environment.
