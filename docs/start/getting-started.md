---
weight: 10
title: Getting Started
description: The fastest path from Check and Result into Flow.
---

# Getting Started

FsFlow is a toolkit for building robust, Result-based programs in F#. It allows you to scale from simple validation logic to complex, effectful application boundaries using a single, unified mental model.

## 1. The Continuum of Logic

FsFlow is designed around a continuum. You should always use the simplest tool that satisfies your current requirement:

```text
Pure Checks -> Result & Validation -> Flow
```

- **Pure Checks**: Reusable predicates for basic validation.
- **Result & Validation**: Domain logic that handles success or failure (either fail-fast or error-accumulating).
- **Flow**: The application boundary where you need dependencies, async/task interop, logging, or cancellation.

## 2. Start with Checks and Results

Most logic starts pure. Use `Check` for reusable predicates and `Result` for domain logic.

```fsharp
open FsFlow
open FsFlow.Check

type UserError = | NameTooShort

let validateName (name: string) : Result<string, UserError> =
    name 
    |> minLength 3 
    |> orError NameTooShort

// This is just a standard F# Result. No magic yet.
let result = validateName "Ad" // Error NameTooShort
```

## 3. Moving to Flow

When your logic needs to interact with the outside world—by calling a database, reading an environment variable, or performing an async task—you move to `Flow`.

A **`Flow<'env, 'error, 'value>`** is a **description of a computation**. It doesn't do anything until you run it.

```fsharp
let greetUser (id: int) : Flow<unit, UserError, string> =
    flow {
        // You can bind a Result directly!
        let! name = validateName "Adam"
        
        // You can perform Async or Task work directly!
        let! (data: string) = async { return $"Hello {name}" }
        
        return data
    }
```

## 4. Execution: Turning Description into Action

Because a `Flow` is just a description, you must explicitly **run** it. This is the boundary where your platform-independent logic meets the real world.

When you call `Flow.run`, you provide the required **environment** (which can be `()` if none is needed) and a cancellation token is handled for you (defaulting to `CancellationToken.None`).
If the flow throws an uncaught exception, the runtime records it as `Cause.Die` in the returned `Exit`.

### Execution Handle vs. Outcome

Because a `Flow` is just a description, you must explicitly **run** it. FsFlow handles the platform differences for you:

`Flow.run` returns an **`Effect<'value, 'error>`**. The platform-specific carrier is defined by the target:

- On **.NET**: `Effect<'value, 'error>` is a `ValueTask<Exit<'value, 'error>>`.
- On **Fable**: `Effect<'value, 'error>` is an `Async<Exit<'value, 'error>>`.

### The `Exit` Outcome

The final result of any flow is an **`Exit<'value, 'error>`**. This type represents every possible outcome:

```fsharp
match exitValue with
| Exit.Success value -> 
    printfn "Success: %A" value

| Exit.Failure (Cause.Fail error) -> 
    printfn "Expected domain error: %A" error

| Exit.Failure (Cause.Die ex) -> 
    printfn "Unexpected defect: %s" ex.Message

| Exit.Failure Cause.Interrupt -> 
    printfn "The workflow was cancelled."
```

Use `Flow.fail` or `Flow.error` for expected domain failures, `Flow.die` for explicit defects, and `Flow.catch` only when you intentionally want to translate an exception into a typed error.

## 5. Running Your First Flow

Here is how you actually execute a flow in a real application:

```fsharp
let myFlow = Flow.succeed "Hello World"

// On .NET:
let exit = Flow.run () myFlow

// On Fable:
let runOnFable () = async {
    let! exit = Flow.run () myFlow
    match exit with
    | Exit.Success s -> printfn "%s" s
    | _ -> ()
}
```

## 6. Reading from the Environment

One of Flow's greatest strengths is managing dependencies without manual parameter passing.

```fsharp
type AppConfig = { ApiUrl: string }

let fetchFromApi : Flow<AppConfig, unit, string> =
    flow {
        // Read just the ApiUrl from the environment record
        let! url = Flow.read _.ApiUrl
        return $"Fetching from {url}..."
    }

// Running with an environment
let config = { ApiUrl = "https://api.example.com" }
let effect = Flow.run config fetchFromApi
```

## Summary: The Flow Lifecycle

1.  **Define**: Use `flow {}` to describe your logic and its requirements.
2.  **Compose**: Combine smaller flows, Results, Tasks, and Asyncs into larger ones.
3.  **Run**: Call `Flow.run env` at your application's entry point (e.g., a Controller or Main function).
4.  **Handle**: Match on the `Exit` value to handle success, failure, or defects.

## Next Steps

- **[Managing Dependencies]({{< relref "/docs/managing-dependencies/" >}})**: Learn how to structure your environments using the Record or CAPS patterns.
- **[Execution Semantics]({{< relref "/docs/core-model/semantics.md" >}})**: Understand short-circuiting, "cold" vs "hot" tasks, and interruption.
- **[Defects and Exceptions]({{< relref "/docs/core-model/defects.md" >}})**: Understand why `Die` is separate from typed failures and how to use it.
- **[Task and Async Interop]({{< relref "/docs/core-model/task-async-interop.md" >}})**: A deep dive into binding different effect types.
