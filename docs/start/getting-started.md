---
weight: 10
title: Getting Started
description: The fastest path from Check and Result into Flow.
---

# Getting Started

Axial is a toolkit for building robust, Result-based programs in F#. It allows you to scale from simple validation logic to complex, effectful application boundaries using a single, unified mental model.

## 1. The Continuum of Logic

Axial is designed around a continuum. You should always use the simplest tool that satisfies your current requirement:

```text
Pure Checks -> Result & Validation -> Flow
```

- **Pure Checks**: Reusable predicates for basic validation.
- **Result & Validation**: Domain logic that handles success or failure (either fail-fast or error-accumulating).
- **Flow**: The application boundary where you need dependencies, async/task interop, logging, or cancellation.

## 2. Start with Checks and Results

Most logic starts pure. Use `Check` for reusable predicates and `Result` for domain logic.

```fsharp
open Axial

type UserError = | NameTooShort

let validateName (name: string) : Result<string, UserError> =
    name 
    |> Check.fromPredicate (fun value -> value.Length >= 3)
    |> Check.withError NameTooShort

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

When you call an execution member such as `ToTask`, `ToAsync`, `ToValueTask`, or `RunSynchronously`, you provide the required **environment** (which can be `()` if none is needed). On .NET, the default cancellation token is `CancellationToken.None`.
If the flow throws an uncaught exception, the runtime records it as `Cause.Die` in the returned `Exit`.

### Execution Handle vs. Outcome

Because a `Flow` is just a description, you must explicitly **run** it. Axial handles the platform differences for you:

The execution handle is target-specific:

- On **.NET**: `Execution<'value, 'error>` is a `ValueTask<Exit<'value, 'error>>`.
- On **Fable**: `Execution<'value, 'error>` is an `Async<Exit<'value, 'error>>`.

### The `Exit` Outcome

The final result of any flow is an **`Exit<'value, 'error>`**. In Axial terms, that is `Result<'value, Cause<'error>>`. We give it its own name because it represents a completed workflow execution, not an ordinary domain result. It covers every possible outcome:

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

Because `ToTask` and `ToAsync` return deferred execution handles, you must await them to get the final `Exit` outcome. On .NET, `RunSynchronously` is the blocking alternative.

```fsharp
let myFlow = Flow.succeed "Hello World"

// On .NET:
let runOnDotNet () = task {
    let! exit = myFlow.ToTask(())
    match exit with
    | Exit.Success s -> printfn "%s" s
    | _ -> ()
}

// On Fable:
let runOnFable () = async {
    let! exit = myFlow.ToAsync(())
    match exit with
    | Exit.Success s -> printfn "%s" s
    | _ -> ()
}
```

For a deeper dive into handling outcomes, cancellation, and combining multiple flows, see **[Execution and Outcomes](./execution-and-outcomes/)**.

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

let runExample () = task {
    let! result = fetchFromApi.ToTask(config)
    printfn "Result: %A" result
}
```

## Summary: The Flow Lifecycle

1.  **Define**: Use `flow {}` to describe your logic and its requirements.
2.  **Compose**: Combine smaller flows, Results, Tasks, and Asyncs into larger ones.
3.  **Run**: Call `RunSynchronously`, `ToTask`, or `ToAsync` at your application's entry point (e.g., a Controller or Main function).
4.  **Handle**: Match on the `Exit` value to handle success, failure, or defects.
