---
weight: 2
title: Getting Started
description: From validated data to workflows with environment, execution, and Exit.
---

# Getting Started

Once your data is validated, you inevitably need the outside world — a database call, an environment variable, an
async task. This is where nesting and manual dependency plumbing usually creep in. `Flow` prevents both: it combines
async execution, typed error tracking, and an environment channel in a single computation expression, so `Result`,
`Async`, and `Task` values bind directly and dependencies are declared in the type instead of threaded by hand.

## 1. The Smallest Flow

Start with the smallest signature that says what the workflow needs.

```fsharp
let hello : Flow<string> =
    flow {
        return "Hello World"
    }
```

A `Flow` is a description of a computation. It does not do anything until you run it.

Suppose the block calls these functions:

```fsharp
let loadUser (id: UserId) : Flow<AppEnv, AppError, User> = ...
let auditUser (user: User) : Flow<AppEnv, AppError, unit> = ...
let sendGreeting (user: User) : Flow<AppEnv, AppError, string> = ...
```

`let!` binds a flow's successful value to the name on its left. `do!` binds a flow returning `unit`.
`return!` uses another flow as the result of the block.

```fsharp
flow {
    let! user = loadUser userId
    do! auditUser user
    return! sendGreeting user
}
```

Here is the same block with the left- and right-hand types shown:

```fsharp
flow {
    let! (user: User) =
        (loadUser userId: Flow<AppEnv, AppError, User>)

    do! (auditUser user: Flow<AppEnv, AppError, unit>)
    return! (sendGreeting user: Flow<AppEnv, AppError, string>)
}
// Flow<AppEnv, AppError, string>
```

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
type UserError = | NameTooShort

let validateName (name: string) : Result<string, UserError> =
    name
    |> Check.minLength 3
    |> Result.orError NameTooShort

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

## 2. Execution

Because a `Flow` is a description, you must explicitly **run** it. This is deliberate: a running task can only be
awaited, but a description can be retried, scheduled, raced, forked, or cancelled by the runtime — and it does nothing
until your application boundary says so.

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

## 3. Running Your First App

Use `App.run` when a Flow is the root of your application. It works on .NET and Fable, carries cancellation from the
calling async, and returns only after the root scope has closed.

```fsharp
let application : Flow<string> = Flow.succeed "Hello World"

let run () = async {
    let! exit = App.run () application
    match exit with
    | Exit.Success s -> printfn "%s" s
    | _ -> ()
}
```

Use `App.start` when a signal handler, desktop window, UI component, or external host will stop the application later.
For complete setup, see [Application lifecycle](./applications/), [.NET hosting](./hosting/),
[Node hosting](./hosting/node/), and [browser hosting](./hosting/browser/).

For individual operations and interop boundaries, `ToTask`, `ToAsync`, `ToValueTask`, and `RunSynchronously` remain
the direct execution interface.

For a deeper dive into handling outcomes, cancellation, and combining multiple flows, see **[Execution and Outcomes](./core-concepts/execution-and-outcomes/)**.

## 4. Reading from the Environment

The environment channel is how Flow ends dependency plumbing. Instead of passing connections, configuration, and
request metadata through every function argument, a workflow declares an `'env` type and reads what it needs; the
concrete environment is supplied once, at the boundary. Because that boundary is the only place the environment is
built, tests swap a live environment for a mock one in a single line — no framework, no container.

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
3.  **Run**: Use `App.run` or `App.start` at an application entry point; use direct execution members for individual operations.
4.  **Handle**: Match on the `Exit` value.
