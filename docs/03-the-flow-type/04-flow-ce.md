---
title: The Flow CE
description: Sequence dependent workflow steps with flow { }.
---

# The Flow CE

Use `flow {}` when later work depends on earlier success.

Suppose the block calls these functions:

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
type User = { Id: int; Name: string }
type AppError = UserNotFound of int
type AppEnv = { Users: Map<int, User>; Audit: ResizeArray<string> }

let loadUser (id: int) : Flow<AppEnv, AppError, User> =
    flow {
        let! users = Flow.envWith _.Users

        match Map.tryFind id users with
        | Some user -> return user
        | None -> return! Flow.fail (UserNotFound id)
    }

let auditUser (user: User) : Flow<AppEnv, AppError, unit> =
    Flow.envWith (fun env -> env.Audit.Add $"read {user.Id}")

let greetUser (user: User) : Flow<AppEnv, AppError, string> = Flow.ok $"Hello, {user.Name}"
```

`let!` binds a successful value to the name on its left. `do!` binds a step whose success value is `unit`.
`return!` uses another complete Flow as the result of the block:

```fsharp
let greet (userId: int) : Flow<AppEnv, AppError, string> =
    flow {
        let! user = loadUser userId
        do! auditUser user
        return! greetUser user
    }
```

The first failure stops the block. `do!` and `return!` do not run for a missing user:

```fsharp run
let env = { Users = Map [ 1, { Id = 1; Name = "Ada" } ]; Audit = ResizeArray() }

greet 1 |> Flow.run env |> shouldEqual (Exit.Success "Hello, Ada")
greet 2 |> Flow.run env |> shouldEqual (Exit.Failure(Cause.Fail(UserNotFound 2)))
List.ofSeq env.Audit |> shouldEqual [ "read 1" ]
```

`flow {}` also binds `Result`, `Option`, `ValueOption`, `Async`, and `ColdTask`. An outer `Result.Error` enters the
Flow error channel. Raw `Task` and `ValueTask` values do not bind directly; use `ColdTask` for work that should start
with the Flow or an explicit `Flow.awaitStarted*` function for work already running. The output remains one cold Flow
description until an execution boundary runs it.

Normal F# `if`, `match`, `for`, and `while` expressions work inside the computation expression.

## Go Further

- [Flow builder reference](/api/) lists the values accepted by each
  computation-expression operation.
- [Bind](/error-handling/bind.html) covers bind-site error assignment and mapping when the source
  error does not already match the workflow.
- [Task and Async interop](/the-flow-type/task-async-interop.html) gives the detailed carrier and
  cancellation rules.
