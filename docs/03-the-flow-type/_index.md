---
title: The Flow Type
description: Create, combine, and run Flow values.
---

# The Flow Type

A Flow is an immutable, cold description of work. Nothing runs until an execution interprets the description with an
environment:

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

```fsharp
type LoadUserError = UserNotFound of int
type AppEnv = { LoadUser: int -> Result<string, LoadUserError> }

let workflow : Flow<AppEnv, LoadUserError, string> =
    flow {
        let! loadUser = Flow.envWith _.LoadUser
        return! loadUser 42
    }
```

Building `workflow` ran nothing. Each execution supplies an environment and returns an outcome:

```fsharp run
let live = { LoadUser = fun id -> Ok $"user {id}" }
let empty = { LoadUser = fun id -> Error(UserNotFound id) }

workflow |> Flow.run live |> shouldEqual (Exit.Success "user 42")
workflow |> Flow.run empty |> shouldEqual (Exit.Failure(Cause.Fail(UserNotFound 42)))
```

The three type parameters say what the workflow needs, how it can fail, and what it produces:

| Parameter | Meaning |
| --- | --- |
| `'env` | Dependencies supplied when the workflow runs |
| `'error` | Expected failures the caller can handle |
| `'value` | The value produced on success |

Aliases such as `Flow<'value>` and `EnvFlow<'env, 'value>` abbreviate the same type with unused channels fixed.

## In this section

1. [Reading the type](flow-type.html): the three channels, the aliases, and what each alias expands to.
2. [Creating flows](creating-flows.html): constructors for values, failures, and interop sources.
3. [Running flows](running-flows.html): executions, outcomes, and boundary conversions.
4. [The flow builder](flow-ce.html): `flow { }` binding rules for flows, tasks, and results.
5. [Combining flows](combining-flows.html): sequencing, mapping, and channel transformations.
6. [Task and async interop](task-async-interop.html): moving between Flow, `Task`, and `Async`.
7. [Troubleshooting types](troubleshooting-types.html): the compiler errors produced when channels do not line up.
8. [Resources](resources.html): acquiring something that must be released.
