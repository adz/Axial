---
weight: 10
title: Result CE
description: Fail-fast composition with the result { } builder.
---

# Result CE

Use `result {}` to connect steps that return `Result`. Each successful step passes its value to the next line, while
the first `Error` becomes the result of the whole block.

## What the keywords do

Suppose the block calls these functions:

```fsharp
let checkName (name: string) : Result<string, UserError> = ...
let checkPermission (input: Input) : Result<unit, UserError> = ...
let save (name: string) : Result<User, UserError> = ...
```

`let!` binds the value inside `Ok` to the name on its left. `do!` binds a step whose successful value is `unit`, so
there is no name on the left. `return!` uses a complete `Result` as the result of the block.

```fsharp
result {
    let! name = checkName input.Name
    do! checkPermission input
    return! save name
}
```

Here is the same block with only the important types shown:

```fsharp
result {
    let! (name: string) =
        (checkName input.Name: Result<string, UserError>)

    do! (checkPermission input: Result<unit, UserError>)

    return! (save name: Result<User, UserError>)
}
// Result<User, UserError>
```

## Basic usage

```fsharp
type UserError = | MissingName | MissingEmail
type User = { Name: string; Email: string }

open Axial.ErrorHandling.CheckDSL

let validateUser name email : Result<User, UserError> =
    result {
        // If name is blank, it returns Error MissingName and stops.
        let! validName = name |> present |> orError MissingName
        
        // This line only runs if the name was valid.
        let! validEmail = email |> present |> orError MissingEmail
        
        return { Name = validName; Email = validEmail }
    }
```

## Options and Checks

`result {}` binds `Result` directly. Use `Result.someOr` when success should take a value out of an option.

A `Check` already returns the checked value on success, so it can appear directly on the right of `let!`.

```fsharp
type User = { Name: string }
type LoginError = MissingPassword | Unauthorized

let tryGetUser username =
    if username = "ada" then Some { Name = username } else None

let login username password =
    result {
        let! user = tryGetUser username |> Result.someOr Unauthorized
        let! _ =
            password
            |> present
            |> orError MissingPassword

        return user
    }
```

## When to use `result {}`

- **Sequential Dependencies**: When Step B requires the output of Step A.
- **Fail-Fast**: When continuing after an error makes no sense (e.g., you can't save a user if the email is invalid).
- **Simple Logic**: When you only need to return a single error value to the caller.

If you need to collect *multiple* independent errors at once, use [`validate {}`]({{< relref "/error-handling/diagnostics/validate-builder.md" >}}) instead.
