---
weight: 3
title: Straightforward Examples
description: Quick, practical examples of Axial in action.
---

# Straightforward Examples

These examples show how to use Axial for common tasks without the overhead of a full application setup.

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
    let! outcome = getUrl.ToTask({ ApiUrl = "https://api.example.com" })
    // outcome = Exit.Success "https://api.example.com"
}
```

## 2. Combining Pure Logic and Async Work

Use `flow {}` to mix pure `Result` logic, `Async` blocks, and other flows.

```fsharp
let validateId id =
    id
    |> Result.require (Check.Number.greaterThan 0)
    |> Result.mapError (fun _ -> "Invalid ID")
    |> Result.map (fun () -> id)

let fetchUser id =
    async { return { Id = id; Name = "Ada" } }

let workflow id =
    flow {
        let! validId = validateId id
        let! user = fetchUser validId // Binds Async<'T> directly
        return user.Name
    }

let runWorkflow () = task {
    let! exit = (workflow 42).ToTask(())
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
    |> Schedule.retry (Schedule.recurs 3)
```

`Schedule.retry` will retry up to 3 times if the task fails with `Cause.Fail`. Defects and interruptions pass through unchanged.

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
