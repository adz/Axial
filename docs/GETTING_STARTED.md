---
weight: 10
title: Getting Started
description: The fastest path from Check and Result into Flow.
---

# Getting Started

This page shows the fastest path from plain checks into the right FsFlow family as the execution context grows.

The core `FsFlow` package contains [`Flow`]({{< relref "/reference/flow/t-flow.md" >}}), [`Check`]({{< relref "/reference/check/t-check.md" >}}), `Result`, [`Validation`]({{< relref "/reference/validation/t-validation.md" >}}), and [`validate {}`]({{< relref "/reference/validation/builders-validate.md" >}}).
The entire task and async surface is unified in the main package.

## 1. Start With The Continuum

FsFlow is meant to scale one Result-based style through richer boundaries:

```text
Pure Checks -> Result & Validation -> Flow
```

Start as small as possible, then lift only when the boundary truly needs more runtime context.
- **Pure Checks**: Reusable predicates using [`Check`]({{< relref "/reference/check/" >}}) that return unit or a value.
- **Result & Validation**: Domain logic that either fails fast (`Result`) or collects multiple errors ([`Validation`]({{< relref "/reference/validation/" >}})).
- **Flow**: The application boundary ([`Flow`]({{< relref "/reference/flow/" >}})) where you need dependencies, async work, or interop.

## 2. Start With Checks And Result

Use **Check** for reusable predicates and **Result** for fail-fast domain logic. Open the `FsFlow.Check` module to use helpers like [`notBlank`]({{< relref "/reference/check/m-checkmodule-notblank.md" >}}) directly:

```fsharp
open FsFlow
open FsFlow.Check

type ValidationError =
    | MissingName

let requireName (name: string) : Result<string, ValidationError> =
    name
    |> notBlank
    |> orError MissingName

let result = requireName "  "
// result = Error MissingName
```

That keeps the pure validation surface small and easy to reuse.

## 3. One Flow to Rule Them All

[`Flow<'env, 'error, 'value>`]({{< relref "/reference/flow/t-flow.md" >}}) is a **data type** that describes a computation. It isn't tied to a specific async runtime during definition. Instead, it is interpreted by the platform when run:

- On **.NET**, it is interpreted using `ValueTask` for maximum performance.
- On **Fable**, it is interpreted using `Async` for JavaScript compatibility.

You never have to change your code when moving between these platforms.

## 4. Handling Effects

You can seamlessly lift and bind different kinds of effects inside a [`flow {}`]({{< relref "/reference/flow/builders-flow.md" >}}) block:

```fsharp
let compositeFlow =
    flow {
        // Lift a standard F# Async
        let! res1 = async { return 42 }
        
        // Lift a .NET Task
        let! res2 = System.Threading.Tasks.Task.FromResult("Hello")
        
        // Bind another Flow
        let! res3 = otherFlow
        
        return $"{res2} {res1}"
    }
```

FsFlow handles the conversion to its internal execution model automatically.

## 6. The Execution Boundary: `Effect` and `Exit`

When you call `Flow.run`, you get back an **`Effect<'value, 'error>`**.
An `Effect` is the cross-platform execution handle.

The **`Exit<'value, 'error>`** type represents the final outcome:
- `Exit.Success value`
- `Exit.Failure (Cause.Fail error)`
- `Exit.Failure Cause.Interrupt` (Canceled)
- `Exit.Failure (Cause.Die exception)` (Defect)

## 7. Read From The Environment

Use [`Flow.read`]({{< relref "/reference/flow/m-flow-read.md" >}}) for projections or [`Flow.env`]({{< relref "/reference/flow/m-flow-env.md" >}}) for the whole environment:

```fsharp
type AppEnv = { Prefix: string }

let greetWithPrefix input : Flow<AppEnv, ValidationError, string> =
    flow {
        let! name = requireName input
        let! prefix = Flow.read _.Prefix
        return $"{prefix} {name}"
    }
```

When application capabilities deserve a name, define a trait set with `Needs<'dep>` and read it with `Env<'dep>`.

## 8. Compose Upward, Not Sideways

Flow is one boundary model. Small sync boundaries can be reused inside async or task-oriented boundaries without any wrapping code.

## 9. What To Read Next

- **[Validation & Results]({{< relref "/docs/validation-results/" >}})**: Learn how to collect multiple errors.
- **[Straightforward Examples]({{< relref "/docs/start/basic-examples/" >}})**: Practical snippets for common tasks.
- **[Managing Dependencies]({{< relref "/docs/core-model/managing-dependencies/" >}})**: Design environment and capability boundaries.
