---
weight: 10
title: Getting Started
description: The fastest path from Check and Result into Flow.
type: docs
---


F# gives us incredibly powerful tools for modelling data, but real-world production systems expose two architectural
gaps in the ecosystem:

1. **The validation dilemma.** Standard `Result` pipelines force a choice between fail-fast logic (which drops every
   error after the first) and applicative validation (which is verbose and detaches errors from the input paths a UI
   needs for redisplay). Either way, nothing stops an invalid object from being constructed before it is checked.
2. **The dependency and infrastructure gridlock.** Mixing validated data with asynchronous side effects — database
   calls, HTTP, telemetry — leads to deeply nested code. Result/async helpers can flatten the nesting, but they still
   leave you manually plumbing infrastructure through every function argument: connections, configuration, trace ids,
   cancellation tokens.

Axial closes both gaps. It unifies plain-`Result` validation helpers, schema-based parsing that makes invalid models
unconstructible, and an environment-aware workflow type (`Flow`) with built-in cancellation, scheduling, and
structured concurrency — one toolkit with one vocabulary.

## 1. What Axial Consists Of

Axial consists of three areas that can be used independently but work together, and they share one vocabulary:

- **[Error Handling]({{< relref "/error-handling/" >}})** — plain `Result` with your own error type for simple,
  pure code. Standard F# `Result` plus a small error union is idiomatic Axial, not a compromise.
- **[Schema]({{< relref "/schema/" >}})** — declare a domain model once; parsing raw input, validation, redisplay,
  contextual rules, and metadata all fall out of that declaration, and an invalid model is never constructed.
- **[Flow]({{< relref "/flow/" >}})** — the effects around them: dependencies, async or task work, cancellation,
  resources, and runtime policy. Flow is optional; the other two areas work without it.

Pick the area that matches the work in front of you:

| Need | Start here |
| :--- | :--- |
| Pure fail-fast logic with your own error type | [Error Handling]({{< relref "/error-handling/" >}}) |
| Parsing forms, CLI args, JSON, or config into trusted models | [Schema]({{< relref "/schema/" >}}) |
| Async, task work, dependencies, resources, or runtime policy | [Flow]({{< relref "/flow/" >}}) |

Everything else — reusable `Check` constraints, accumulating `Validation` diagnostics, `Refined` single-value parsing
— is machinery inside those areas. Reach for it directly only when it clearly pays for itself; the
[Choosing A Tool]({{< relref "/schema/choosing-a-tool/" >}}) guide maps the full ladder when you want it.

## 2. Simple Code: Plain Results

Most logic starts pure, and most checks don't deserve a framework. Plain `Result` with your own error union is the
blessed approach for code without a domain model — Axial's helpers remove the guard-clause boilerplate, and no Axial types
appear in your signatures.

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

When the input is a whole model rather than one value, checking values one by one falls apart: fail-fast drops sibling
errors, hand-rolled accumulation loses the field paths, and either way the record gets constructed before the checks
finish. Instead, declare a schema once and parse raw input through it. If any constraint fails, the model is never
constructed — you get path-aware errors for every failing field, and the original input is retained for redisplay.

```fsharp
open Axial.Schema
open Axial.Validation.Schema

type Signup = { Email: string; Age: int }

let signupSchema =
    Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
    |> Schema.fieldWith
        [ SchemaConstraint.required; SchemaConstraint.email ]
        "email" _.Email Value.text
    |> Schema.fieldWith [ SchemaConstraint.atLeast 13 ] "age" _.Age Value.int
    |> Schema.build

let raw = RawInput.ofNameValues [ "email", "ada@example.com"; "age", "36" ]
let parsed = Input.parse signupSchema raw

match parsed.Result with
| Ok signup -> printfn "trusted: %A" signup
| Error _ -> printfn "rejected: %A" parsed.Errors   // path-aware; raw input kept in parsed.Input
```

The same schema also re-validates existing values, powers contextual rules, and describes itself to JSON Schema, docs,
and UI interpreters. Start with the [Schema tutorials]({{< relref "/schema/tutorials/" >}}).

## 4. Moving to Flow

Once your data is validated, you inevitably need the outside world — a database call, an environment variable, an
async task. This is where nesting and manual dependency plumbing usually creep in. `Flow` prevents both: it combines
async execution, typed error tracking, and an environment channel in a single computation expression, so `Result`,
`Async`, and `Task` values bind directly and dependencies are declared in the type instead of threaded by hand.

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

For a deeper dive into handling outcomes, cancellation, and combining multiple flows, see **[Execution and Outcomes]({{< relref "/flow/execution-and-outcomes/" >}})**.

## 7. Reading from the Environment

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
3.  **Run**: Call `RunSynchronously`, `ToTask`, or `ToAsync` at an application entry point.
4.  **Handle**: Match on the `Exit` value.
