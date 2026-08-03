---
weight: 10
title: Result CE
description: Fail-fast composition with the result { } builder.
type: docs
---


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

open Axial.Constraint.ConstraintDSL

let validateUser name email : Result<User, UserError> =
    result {
        // If name is blank, it returns Error MissingName and stops.
        let! validName = name |> Result.guard present |> Result.mapError (fun _ -> MissingName)
        
        // This line only runs if the name was valid.
        let! validEmail = email |> Result.guard present |> Result.mapError (fun _ -> MissingEmail)
        
        return { Name = validName; Email = validEmail }
    }
```

## Options and Checks

`result {}` binds `Result` directly. Use `Result.someOr` when success should take a value out of an option.

`Constraint.guard` already returns the checked value on success, so it can appear directly on the right of `let!`.

```fsharp
type User = { Name: string }
type LoginError = MissingPassword | Unauthorized

let tryGetUser username =
    if username = "ada" then Some { Name = username } else None

let login username password =
    result {
        let! user = tryGetUser username |> Result.someOr Unauthorized
        do!
            password
            |> present
            |> Result.mapError (fun _ -> MissingPassword)

        return user
    }
```

## Collecting every error with `and!`

`result {}` stops at the first `Error`. When the steps do not depend on each other and the caller should see every
failure, use one of the accumulating builders and join the independent bindings with `and!`.

```fsharp
result.list {
    let! name = parseName input.Name
    and! age = parseAge input.Age
    return name, age
}
// Result<string * int, string list>
```

Both bindings run. If both fail, both errors appear in the list.

The builder name chooses the container the errors collect into, and that container shows up in the error type:

| Builder | Result type |
| --- | --- |
| `result.list { }` | `Result<'value, 'error list>` |
| `result.array { }` | `Result<'value, 'error[]>` |

Each accepts ordinary `Result<'value, 'error>` bindings and lifts each error into the container for you. A binding
that already carries the collected type passes through without being wrapped again, so results from two accumulating
blocks compose.

### `and!` accumulates; `let!` still fails fast

The two keywords mean different things in the same block. Bindings joined by `and!` are independent, so all of them
run and their errors combine. A following `let!` depends on what came before it, so it cannot run until the earlier
bindings succeed.

```fsharp
result.list {
    let! name = parseName input.Name
    and! age = parseAge input.Age    // runs even when parseName fails; both errors collect

    let! account = loadAccount name  // only runs once name and age both succeeded
    return account, age
}
```

If `parseName` and `parseAge` both fail, the block returns both errors. If `parseName` fails, `loadAccount` never
runs and its error cannot appear. Group everything you want reported together into one `and!` chain.

## When to use `result {}`

- **Sequential Dependencies**: When Step B requires the output of Step A.
- **Fail-Fast**: When continuing after an error makes no sense (e.g., you can't save a user if the email is invalid).
- **Simple Logic**: When you only need to return a single error value to the caller.

Use `result.list {}` and its siblings when independent steps should all report. They collect a flat container of
errors with no path information. Map to another container with `Result.mapError` when you need one.

For complete failures across independent fields, declare those fields in
[Schema]({{< relref "/schema/" >}}). Schema supplies field and collection paths automatically.
