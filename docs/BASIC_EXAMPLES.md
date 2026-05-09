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

let result = getUrl |> Flow.run { ApiUrl = "https://api.example.com" }
// Ok "https://api.example.com"
```

## 2. Combining Sync and Async Work

Use `asyncFlow {}` to mix pure functions, `Async` blocks, and other flows.

```fsharp
let validateId id =
    if id > 0 then Ok id else Error "Invalid ID"

let fetchUser id =
    async { return { Id = id; Name = "Ada" } }

let workflow id =
    asyncFlow {
        let! validId = validateId id
        let! user = fetchUser validId
        return user.Name
    }

let result = workflow 42 |> AsyncFlow.run ()
// Async<Result<string, string>>
```

## 3. Retrying a Task

Use the `TaskFlow.Runtime` module to add operational policies like retries.

```fsharp
open FsFlow.Net

let flakyTask =
    taskFlow {
        // Imagine this calls a flaky API
        return! Task.FromResult (Ok "Success")
    }

let resilientWorkflow =
    flakyTask
    |> TaskFlow.Runtime.retry (RetryPolicy.constant (TimeSpan.FromSeconds 1) 3)

// Will retry up to 3 times with a 1-second delay
```

## 4. Conditional Execution

Since FsFlow builders are just F# computation expressions, you can use standard `if/then` logic inside them.

```fsharp
let conditionalWorkflow input =
    taskFlow {
        if String.IsNullOrWhiteSpace input then
            return "No input provided"
        else
            let! processed = processInput input
            return processed
    }
```

## 5. Mapping Errors

Use `mapError` to translate low-level errors into high-level domain errors.

```fsharp
let domainWorkflow =
    lowLevelFlow
    |> TaskFlow.mapError (function
        | DbError _ -> DatabaseUnavailable
        | NetworkError _ -> ExternalServiceDown)
```
