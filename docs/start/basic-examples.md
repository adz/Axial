---
weight: 30
title: Straightforward Examples
description: Quick, practical examples of FsFlow in action.
---

# Straightforward Examples

These examples show how to use FsFlow for common tasks without the overhead of a full application setup.

## 1. Simple Environment Access

Use `Flow.read` to project a single field from your environment record.

```fsharp
type Config = { ApiUrl: string }

let getUrl =
    flow {
        let! url = Flow.read _.ApiUrl
        return url
    }

let run () = task {
    let! outcome = getUrl |> Flow.run { ApiUrl = "https://api.example.com" }
    // outcome = Exit.Success "https://api.example.com"
}
```

## 2. Combining Pure Logic and Async Work

Use `flow {}` to mix pure `Check` logic, `Async` blocks, and other flows.

```fsharp
let validateId id =
    id |> okIf (id > 0) |> orError "Invalid ID"

let fetchUser id =
    async { return { Id = id; Name = "Ada" } }

let workflow id =
    flow {
        let! validId = validateId id
        let! user = fetchUser validId // Binds Async<'T> directly
        return user.Name
    }

let runWorkflow () = task {
    let! exit = workflow 42 |> Flow.run ()
    // exit = Exit.Success "Ada"
}
```

## 3. Retrying a Flow

Use the `Schedule` module to add operational policies like retries.

```fsharp
let mutable attempts = 0

let flakyTask =
    flow {
        attempts <- attempts + 1

        if attempts < 2 then
            return! Flow.fail "temporary-error"
        else
            return "Success"
    }

let resilientWorkflow =
    flakyTask
    |> Flow.Retry (Schedule.recurs 3)

[`Flow.Retry`]({{< relref "/reference/flow/m-flowschedule-retry.md" >}}) will retry up to 3 times if the task fails with `Cause.Fail`. Defects and interruptions pass through unchanged.
```

## 4. Conditional Execution

Since `flow {}` is a standard F# computation expression, you can use `if/then`, `match`, and `try/with` inside it.

```fsharp
let conditionalWorkflow input =
    flow {
        if String.IsNullOrWhiteSpace input then
            return "No input provided"
        else
            let! processed = processInput input
            return processed
    }
```

## 5. Mapping Errors

Use `Flow.mapError` to translate low-level technical errors into domain-specific failures.

```fsharp
type AppError = DatabaseUnavailable | UserNotFound

let domainWorkflow =
    lowLevelFlow
    |> Flow.mapError (function
        | DbException _ -> DatabaseUnavailable
        | :? KeyNotFoundException -> UserNotFound
        | _ -> DatabaseUnavailable)
```
