---
weight: 20
title: Result CE
description: Fail-fast composition with the result { } builder.
---

# Result CE

Use the `result {}` computation expression when you have a sequence of steps where each step depends on the previous one, and you want to **stop at the first failure**.

This is "fail-fast" semantics.

## Basic Usage

The `result {}` builder binds standard F# `Result<'value, 'error>` types.

```fsharp
type UserError = | MissingName | MissingEmail
type User = { Name: string; Email: string }

let validateUser name email : Result<User, UserError> =
    result {
        // If name is blank, it returns Error MissingName and stops.
        let! validName = name |> Take.whenNotBlank |> Check.withError MissingName
        
        // This line only runs if the name was valid.
        let! validEmail = email |> Take.whenNotBlank |> Check.withError MissingEmail
        
        return { Name = validName; Email = validEmail }
    }
```

## Options and Checks

`result {}` binds `Result` directly.
Use `Take` when the source must expose a value, and `Check` when the source is only a yes/no gate.

```fsharp
type User = { Name: string }
type LoginError = MissingPassword | Unauthorized

let tryGetUser username =
    if username = "ada" then Some { Name = username } else None

let login username password =
    result {
        let! user = tryGetUser username |> Take.some |> Check.withError Unauthorized
        do! password |> Check.notBlank |> Check.withError MissingPassword

        return user
    }
```

## When to use `result {}`

- **Sequential Dependencies**: When Step B requires the output of Step A.
- **Fail-Fast**: When continuing after an error makes no sense (e.g., you can't save a user if the email is invalid).
- **Simple Logic**: When you only need to return a single error value to the caller.

If you need to collect *multiple* independent errors at once, use [`validate {}`](../validate-ce/) instead.
