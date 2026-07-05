---
weight: 10
title: Getting Started
description: The fastest path from Check and Result into Flow.
---

# Getting Started

Axial is a toolkit for Result-based programs in F#. It starts with validation helpers and extends to application boundaries that need dependencies, async work, cancellation, or runtime policy.

## 1. The Two-Lane Rule

Axial is two tools that share one vocabulary. Pick your lane by asking one question: **am I modelling a domain?**

- **Yes — declare a [Schema](../../schema/).** Parsing raw input, validation, redisplay, contextual rules, and
  metadata all fall out of one declaration, and an invalid model is never constructed.
- **No — use plain `Result` with your own error type.** Standard F# `Result` plus a small error union is idiomatic
  Axial, not a compromise.

When the code around either lane needs dependencies, async or task work, cancellation, or runtime policy, lift it
into [Flow](../../flow/) — the effects side of the library. Flow is optional; both lanes work without it.

| Need | Start here |
| :--- | :--- |
| Parsing forms, CLI args, JSON, or config into trusted models | [Schema](../schema/) |
| Pure fail-fast logic with your own error type | [Error Handling](../error-handling/) |
| Async, task work, dependencies, resources, or runtime policy | [Flow](../flow/) |

Everything else — reusable `Check` constraints, accumulating `Validation` diagnostics, `Refined` single-value parsing
— is machinery behind those doors. Reach for it directly only when it clearly pays for itself; the
[Choosing A Tool](../../schema/choosing-a-tool/) guide maps the full ladder when you want it.

## 2. Simple Code: Plain Results

Most logic starts pure. Plain `Result` with your own error union is the blessed lane for code without a domain model
— no Axial types required in your signatures.

```fsharp
open Axial

type UserError = | NameTooShort

let validateName (name: string) : Result<string, UserError> =
    name 
    |> Result.minLength 3
    |> Result.mapError (fun _ -> NameTooShort)

// This is a standard F# Result.
let result = validateName "Ad" // Error NameTooShort
```

## 3. Parse a Form into a Trusted Model

When the input is a whole model rather than one value, declare a schema once and parse raw input through it. If any
constraint fails, the model is never constructed — you get path-aware errors and the original input for redisplay.

```fsharp
open Axial.Schema
open Axial.Validation.Schema

type Signup = { Email: string; Age: int }

let signupSchema =
    Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
    |> Schema.fieldWith
        [ SchemaConstraint.required; SchemaConstraint.email ]
        "email" _.Email Value.text
    |> Schema.fieldWith [ SchemaConstraint.atLeast 13 ] "age" _.Age Value.``int``
    |> Schema.build

let raw = RawInput.ofNameValues [ "email", "ada@example.com"; "age", "36" ]
let parsed = Input.parse signupSchema raw

match parsed.Result with
| Ok signup -> printfn "trusted: %A" signup
| Error _ -> printfn "rejected: %A" parsed.Errors   // path-aware; raw input kept in parsed.Input
```

The same schema also re-validates existing values, powers contextual rules, and describes itself to JSON Schema, docs,
and UI interpreters. Start with the [Schema tutorials](../../schema/tutorials/).

## 4. Moving to Flow

When your logic needs to interact with the outside world—by calling a database, reading an environment variable, or performing an async task—you move to `Flow`.

Start with the smallest signature that says what the workflow needs.

```fsharp
let hello : Flow<string> =
    flow {
        return "Hello World"
    }
```

A `Flow` is a description of a computation. It does not do anything until you run it.

The shortest forms remove channels you are not using:

| Alias | Meaning |
| :--- | :--- |
| `Flow<'value>` | No environment and no typed failure. |
| `Flow<'error, 'value>` | No environment, with a typed failure channel. |
| `EnvFlow<'env, 'value>` | Environment, with no typed failure. |
| `ExnFlow<'value>` | No environment, with recoverable exceptions in the typed failure channel. |
| `ExnEnvFlow<'env, 'value>` | Environment, with recoverable exceptions in the typed failure channel. |

Use the full `Flow<'env, 'error, 'value>` shape when a workflow needs both an environment and a typed failure channel.

```fsharp
let greetUser (id: int) : Flow<UserError, string> =
    flow {
        // Result can be bound directly.
        let! name = validateName "Adam"
        
        // Async and Task values can be bound directly.
        let! (data: string) = async { return $"Hello {name}" }
        
        return data
    }
```

The example above has a typed failure channel but no environment, so it uses `Flow<UserError, string>`.

## 5. Execution

Because a `Flow` is a description, you must explicitly **run** it.

When you call an execution member such as `ToTask`, `ToAsync`, `ToValueTask`, or `RunSynchronously`, you provide the required **environment** (which can be `()` if none is needed). On .NET, the default cancellation token is `CancellationToken.None`.
If the flow throws an uncaught exception, the runtime records it as `Cause.Die` in the returned `Exit`.

### Execution Handle vs. Outcome

The execution handle is target-specific:

- On **.NET**: `Execution<'value, 'error>` is a `ValueTask<Exit<'value, 'error>>`.
- On **Fable**: `Execution<'value, 'error>` is an `Async<Exit<'value, 'error>>`.

### The `Exit` Outcome

The final result of any flow is an **`Exit<'value, 'error>`**. In Axial terms, that is `Result<'value, Cause<'error>>`. It has its own name because it represents a completed workflow execution, not an ordinary domain result.

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

Use `Flow.fail` or `Flow.error` for expected domain failures, `Flow.die` for explicit defects, and `Flow.catch` only when you intentionally want to translate a defect into a typed error. Use `Flow.attemptTask`, `Flow.attemptValueTask`, or `Flow.attemptAsync` when thrown exceptions are expected interop outcomes.

## 6. Running Your First Flow

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

For a deeper dive into handling outcomes, cancellation, and combining multiple flows, see **[Execution and Outcomes](../flow/execution-and-outcomes/)**.

## 7. Reading from the Environment

Flow can read dependencies from an explicit environment.

```fsharp
type AppConfig = { ApiUrl: string }

let fetchFromApi : EnvFlow<AppConfig, string> =
    flow {
        // Read ApiUrl from the environment record.
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

## Summary

1.  **Define**: Use `flow {}` to describe the work and its requirements.
2.  **Compose**: Combine flows, Results, Tasks, and Asyncs.
3.  **Run**: Call `RunSynchronously`, `ToTask`, or `ToAsync` at an application entry point.
4.  **Handle**: Match on the `Exit` value.
