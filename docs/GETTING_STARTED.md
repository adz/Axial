---
weight: 10
title: Getting Started
description: The fastest path from Check and Result into Flow.
---

# Getting Started

This page shows the fastest path from plain checks into the right FsFlow family as the execution context grows.

The core `FsFlow` package contains Flow, Check, Result, Validation, and [`validate {}`]({{< relref "/reference/validation/builders-validate.md" >}}).
The entire task and async surface is now unified in the main `FsFlow` package.

## 1. Start With The Continuum

FsFlow is meant to scale one Result-based style through richer boundaries:

```text
Check -> Result -> Validation -> Flow
```

Start as small as possible, then lift only when the boundary truly needs more runtime context.

## 2. Start With Checks And Result

Use **Check** for reusable predicates and **Result** for fail-fast domain logic:

```fsharp
open FsFlow

type ValidationError =
    | MissingName

let requireName (name: string) : Result<string, ValidationError> =
    name
    |> Check.notBlank
    |> Check.orError MissingName

let result = requireName "  "
// result = Error MissingName
```

That keeps the pure validation surface small and easy to reuse.

When the same source needs to cross into `flow {}`, use `Guard.Of` or `Guard.MapError`.
Guard keeps the source value visible to the computation expression and carries the failure value alongside it:

```fsharp
let login username password =
    flow {
        let! user = tryGetUser username |> Guard.MapError MissingName
        do! Check.notBlank password |> Guard.Of InvalidPassword
        return user
    }
```

## 3. Add Validation When Siblings Are Independent

Use **Validation** and [`validate {}`]({{< relref "/reference/validation/builders-validate.md" >}}) when you want sibling checks to accumulate instead of stopping at the first one:

```fsharp
type Registration = { Name: string; Email: string }
type RegistrationError = NameRequired | EmailRequired

let validateRegistration (input: Registration) : Validation<Registration, RegistrationError> =
    validate {
        let! name = input.Name |> Check.notBlank |> Check.orError NameRequired
        and! email = input.Email |> Check.notBlank |> Check.orError EmailRequired
        return { Name = name; Email = email }
    }

let v = validateRegistration { Name = ""; Email = "" }
// v = Validation (Error { Errors = [NameRequired; EmailRequired]; Children = [] })
```

Use [`result {}`]({{< relref "/reference/result/" >}}) when the next step depends on the previous one and should stop immediately on failure.

## 4. One Flow to Rule Them All

FsFlow 1.0 provides a single **`Flow<'env, 'error, 'value>`** type that handles synchronous code, F# `Async`, and .NET `Task` interop natively within the same `flow {}` builder.

You no longer need to switch types based on the async runtime. Pick the boundary that matches your dependencies.

## 5. Use Flow For Synchronous Boundaries

Use Flow when the computation needs dependencies and typed failure, but no async runtime:

```fsharp
type AppEnv = { Prefix: string }

let greet input : Flow<AppEnv, ValidationError, string> =
    flow {
        let! name = requireName input
        let! prefix = Flow.read _.Prefix
        return $"{prefix} {name}"
    }

let outcome = greet "Ada" |> Flow.run { Prefix = "Hello" }
// outcome = Exit.Success "Hello Ada"
```

## 6. Use Flow For `Async`-Based Boundaries

Use Flow when the computation body naturally binds F# `Async`:

```fsharp
type AsyncEnv = { LoadName: int -> Async<string> }

let greetAsync userId : Flow<AsyncEnv, ValidationError, string> =
    flow {
        let! loadName = Flow.read _.LoadName
        let! name = loadName userId // Direct bind of Async<'T>
        return $"Hello, {name}"
    }

let effect = greetAsync 42 |> Flow.run { LoadName = fun _ -> async { return "Ada" } }
// effect = Effect<string, ValidationError> (ValueTask on .NET, Async on Fable)
```

## 7. Use Flow For `.NET Task`-Based Boundaries

Use Flow when the computation body is task-oriented end to end:

```fsharp
open System.Threading.Tasks

type TaskEnv = { LoadName: int -> Task<string> }

let greetTask userId : Flow<TaskEnv, ValidationError, string> =
    flow {
        let! loadName = Flow.read _.LoadName
        let! name = loadName userId // Direct bind of Task<'T>
        return $"Hello, {name}"
    }
```

FsFlow handles the conversion to its internal `Effect` model automatically.

## 8. The Execution Boundary: `Effect` and `Exit`

When you call `Flow.run`, you get back an **`Effect<'value, 'error>`**.
An `Effect` is the cross-platform execution handle:
- On **.NET**, it is a `ValueTask<Exit<'value, 'error>>`.
- On **Fable**, it is an `Async<Exit<'value, 'error>>`.

The **`Exit<'value, 'error>`** type represents the final outcome:
- `Exit.Success value`
- `Exit.Failure (Cause.Fail error)`
- `Exit.Failure Cause.Interrupt` (Canceled)
- `Exit.Failure (Cause.Die exception)` (Defect)

## 9. Read From The Environment

Use `Flow.read` for projections or `Flow.env` for the whole environment:

```fsharp
let greetWithPrefix input : Flow<AppEnv, ValidationError, string> =
    flow {
        let! name = requireName input
        let! prefix = Flow.read _.Prefix
        return $"{prefix} {name}"
    }
```

When application capabilities deserve a name, define a trait set with `Needs<'dep>` and read it with `Env<'dep>`.

## 10. Compose Upward, Not Sideways

Flow is one boundary model. Small sync boundaries can be reused inside async or task-oriented boundaries without any wrapping code:

```fsharp
let validateGreeting input : Flow<AppEnv, ValidationError, string> =
    flow {
        let! name = requireName input
        return name
    }

let greetTaskValidated input : Flow<TaskEnv, ValidationError, string> =
    flow {
        let! validName = validateGreeting input
        let! prefix = Flow.read _.Prefix
        return $"{prefix} {validName}"
    }
```

## 11. What To Read Next

- **[Validation & Results](../validation-results/)**: Learn the full story from pure checks to structured diagnostics.
- **[Straightforward Examples](./basic-examples/)**: Practical snippets for common tasks.
- **[Managing Dependencies](../core-model/managing-dependencies/)**: Design environment and capability boundaries.
