---
weight: 30
title: The FsFlow Model
description: The core FsFlow progression from Check and Result into Validation and Flow.
---


# The FsFlow Model

This page shows why FsFlow is best understood as one scalable model for Result-based programs rather than a small helper for application boundaries.

The core progression is:

```text
Pure Checks -> Result & Validation -> Flow
```

The validation vocabulary stays the same while the execution context grows.

- start with **Pure Checks** for reusable predicates
- move to **Result & Validation** for fail-fast domain logic or accumulating errors
- lift into **Flow** only when the boundary needs explicit environment access or becomes asynchronous

That matters because many F# codebases end up with separate worlds:

```fsharp
Result<'value, 'error>
Validation<'value, 'error>
Async<Result<'value, 'error>>
Task<Result<'value, 'error>>
```

Those shapes work, but they often split the same program across separate helper modules, separate builders,
and repeated adaptation between pure validation and effectful orchestration.

FsFlow gives those shapes one coherent family:

```fsharp
Check<'value>
Result<'value, 'error>
Validation<'value, 'error>
Flow<'env, 'error, 'value>
```

The point is not to replace Result, `Async`, or `Task`.
The point is to let one Result-based style scale into real application boundaries without changing the mental model.

Flow is the boundary model. The same builder can bind sync values, `Async`, `Task`, `ValueTask`, and `ColdTask` directly.
Guard is the explicit bridge that keeps check-like sources and existing error-bearing sources readable
when they need to enter `flow {}`.

## The Main Claim

FsFlow unifies Result-based programming across pure logic and effectful execution.

- write predicate logic once with Check, using value-preserving checks when you need the input again and gate checks when you only need yes/no
- keep fail-fast domain logic in Result
- accumulate independent validation with Validation
- lift the same logic directly into flows when you need environment, async, task, cancellation, logging, or resource handling
- keep the smallest honest runtime shape at each boundary

## Before The Runtime Grows

Start with a plain check or validation helper:

```fsharp
open FsFlow

type RegistrationError =
    | NameMissing
    | EmailMissing

let validateEmail (email: string) : Result<string, RegistrationError> =
    email
    |> notBlank
    |> orError EmailMissing
```

This is already enough for pure code and should stay plain when the surrounding logic is still plain.

If independent checks should accumulate, move to Validation instead of forcing everything through Result:

```fsharp
let validateRegistration (email: string) (name: string) : Validation<string * string, RegistrationError> =
    validate {
        let! validEmail = validateEmail email
        and! validName = Check.notBlank name |> Check.orError NameMissing
        return validEmail, validName
    }
```

## When The Boundary Grows

When that same use case needs dependencies and task work, keep the validation as-is and lift the boundary:

```fsharp
open System.Threading.Tasks

type RegistrationEnv =
    { LoadUser: int -> Task<Result<User, RegistrationError>>
      SaveUser: User -> Task<Result<unit, RegistrationError>> }

let register userId : Flow<RegistrationEnv, RegistrationError, unit> =
    flow {
        let! loadUser = Flow.read _.LoadUser
        let! saveUser = Flow.read _.SaveUser

        let! user = loadUser userId
        do! validateEmail user.Email
        return! saveUser user
    }
```

`validateEmail` is still just `Result<string, RegistrationError>`.
There is no separate task-result validation vocabulary to switch to first.

## What This Replaces

FsFlow is strongest when you would otherwise spread the same use case across:

- plain Check, Result, and Validation helpers
- `Async<Result<_,_>>` or `Task<Result<_,_>>` wrappers
- extra helper modules for each wrapper shape
- manual environment threading or ad hoc service lookups

Instead, the same logic can move upward through the computation families while keeping the same typed-failure story.

## Adoption Rule

Use FsFlow by default in the effectful application layer where the boundary genuinely needs more than plain Result:

- handlers
- use cases
- service orchestration
- infrastructure-facing application services

Keep the domain plain F# by default:

- domain models
- pure business rules
- small validation helpers
- plain Result when it already reads clearly

## Short-Circuiting Is Intentional

Check, Result, Validation, and Flow are short-circuiting.
They stop on the first typed failure.

That is a feature, not a missing applicative layer.

If you need accumulated validation, use Validation and [`validate {}`]({{< relref "/reference/validation/builders-validate.md" >}}) explicitly.
FsFlow does not try to hide that behavior inside the workflow builders.

## What Keeps It Readable

The design stays explicit in the places that matter for teams:

- env access is visible through [`Flow.read`]({{< relref "/reference/flow/environment/m-flow-read.md" >}})
- execution is visible through [`RunSynchronously`]({{< relref "/reference/flow/execution/m-flow-runsynchronously.md" >}}) and the other execution members
- expected failures stay in the type
- the computation family tells you whether the use case is sync, `Async`, or `.NET Task`

This keeps the code close to ordinary F# application code instead of turning each runtime shape into a new mini-ecosystem.

## Why This Is Low Risk

Adopting FsFlow does not mean betting on a replacement runtime.

- the underlying async and task work still runs on F# `Async` and `.NET Task`
- execution is still explicit
- the library stays narrow and DX-focused rather than growing into a concurrency platform

The goal is not to compete with the BCL or the F# core library.
The goal is to make mixed application computations easier to write and easier to read.

## When Not To Use It

Do not introduce FsFlow early just because a dependency might appear later.

Stay with plain F# when:

- the code is mostly pure
- a direct function parameter is clearer
- plain Result already says everything
- Validation or a plain `Task<'T>` / `Async<'T>` boundary is the simplest honest shape
