---
title: Policy and verification
type: docs
description: Define reusable, environment-aware verification rules and run them with Flow.verify.
---

# Policy and verification

Use a `Policy` to define a named verification rule that a workflow can apply to an input value. Run the rule inside a
workflow with `Flow.verify`.

A policy has this shape:

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp no-check reason="A type on its own, not an expression"
Policy<'env, 'error, 'input, 'output>
```

It is an alias for a function:

```fsharp no-check reason="A function type on its own, not an expression"
'env -> 'input -> Result<'output, 'error>
```

The type parameters describe the policy's contract:

- `'env` is the workflow environment that the policy can read.
- `'error` is the expected workflow error returned when verification fails.
- `'input` is the value to verify.
- `'output` is the verified or transformed value returned on success.

Defining a policy does not run a Flow. A policy is a reusable function value. `Flow.verify policy input` creates a
Flow that supplies the current workflow environment to the policy when the Flow runs. `Ok output` continues the
workflow, and `Error error` short-circuits it through the typed error channel.

## Define and run a policy

The following policy checks a limit from the workflow environment:

```fsharp
type AppEnv =
    { EnforceLimit: bool
      Limit: int }

type OrderError = TooLarge

let withinLimit : Policy<AppEnv, OrderError, int, int> =
    fun env count ->
        if count <= env.Limit then Ok count
        else Error TooLarge

let placeOrder count =
    flow {
        let! checkedCount =
            count
            |> Flow.verify withinLimit

        return checkedCount
    }
```

```fsharp run
placeOrder 3 |> Flow.run { EnforceLimit = true; Limit = 5 } |> shouldEqual (Exit.Success 3)
placeOrder 9 |> Flow.run { EnforceLimit = true; Limit = 5 } |> shouldEqual (Exit.Failure(Cause.Fail TooLarge))
```

`Flow.verify` is pipe-friendly because it takes the policy first and the input second. The example is equivalent to
`Flow.verify withinLimit count`.

## Adapt an existing function

Use a `Policy` constructor when you already have a function that returns `Result`:

| Function | Use it when |
| --- | --- |
| `Policy.lift operation mapError` | The operation does not need the environment and its error must be mapped. |
| `Policy.withError operation error` | The operation does not need the environment and any failure has one workflow error. |
| `Policy.context operation mapError` | The operation reads the environment and its error must be mapped. |

For example, `Policy.withError` assigns a workflow error to a validation function whose error is `unit`:

```fsharp
type RegistrationError = NameRequired

let requireNonBlank (value: string) =
    if String.IsNullOrWhiteSpace value then Error() else Ok value

let requireName : Policy<unit, RegistrationError, string, string> =
    Policy.withError requireNonBlank NameRequired

let register (name: string) : Flow<RegistrationError, string> =
    flow {
        let! checkedName = name |> Flow.verify requireName
        return checkedName
    }
```

```fsharp run
register "Ada" |> Flow.run () |> shouldEqual (Exit.Success "Ada")
register "  " |> Flow.run () |> shouldEqual (Exit.Failure(Cause.Fail NameRequired))
```

## Compose policies

Use `Policy.compose first second` to run two policies from left to right. The second policy receives the successful
output of the first. The first error stops the composition.

```fsharp
let normalizeName : Policy<unit, RegistrationError, string, string> =
    Policy.lift (fun (name: string) -> Ok(name.Trim())) id

let normalizedName = Policy.compose requireName normalizeName
```

```fsharp run
" Ada " |> Flow.verify normalizedName |> Flow.run () |> shouldEqual (Exit.Success "Ada")
" " |> Flow.verify normalizedName |> Flow.run () |> shouldEqual (Exit.Failure(Cause.Fail NameRequired))
```

Both policies must use the same environment and error types. The first policy's output type must match the second
policy's input type.

`Policy.pass` returns its input unchanged. Use it when a composition requires a policy but no verification is needed.

## Enable a policy from the environment

Use `Policy.optional enabled policy` when the environment decides whether a policy applies:

```fsharp
let orderLimit =
    withinLimit
    |> Policy.optional _.EnforceLimit
```

```fsharp run
9 |> Flow.verify orderLimit |> Flow.run { EnforceLimit = false; Limit = 5 } |> shouldEqual (Exit.Success 9)
9 |> Flow.verify orderLimit |> Flow.run { EnforceLimit = true; Limit = 5 } |> shouldEqual (Exit.Failure(Cause.Fail TooLarge))
```

When `enabled env` is `true`, the policy runs. When it is `false`, the policy returns the input unchanged. For this
reason, `Policy.optional` requires the policy's input and output types to be the same.

## Choose between Policy and Bind

Use `Policy` when a verification rule has a domain name, appears in multiple workflows, reads the environment,
composes with other rules, or can be enabled by configuration.

Use [Bind](/error-handling/bind.html) when one `let!`, `do!`, or `return!` site only needs to assign or map the error
of its source. `Bind` produces a computation-expression marker; a policy is a reusable function that `Flow.verify`
runs as a workflow step.
