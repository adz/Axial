---
weight: 85
title: Runnable Examples
description: Executable workflow boundary examples mirrored back into the docs.
---

# Runnable Examples

This page shows the examples that are executed during the docs build, so the public docs stay tied to real code and observed output.

The examples below are built from the repository projects, run with the current source, and then written back into this page.

The code blocks keep the important API calls on the same lines as the values they bind, with trailing comments where that makes the signature easier to read.
The examples prefer the normal direct-bind style inside computation expressions, so the docs reflect the recommended day-to-day usage.

## Request Boundary Example

This example shows a request boundary that pulls a user from a database-like environment, threads a trace id through the request context, and reuses the same validation shape across Flow.

Run it:

```bash
AXIAL_EXAMPLE=request-boundary dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo
```

Source:

- [RequestBoundaryExample.fs](https://github.com/adz/Axial/blob/main/examples/Axial.Examples/RequestBoundaryExample.fs)

Source code:

```fsharp
module RequestBoundaryExample

open System
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.ErrorHandling
open Axial.Validation

type User =
    { Id: int
      Name: string }

type AppDb =
    { FindUser: int -> User option }

type RequestEnv =
    { TraceId: Guid
      Prefix: string
      Db: AppDb
      LoadSuffix: Task<string> }

let validateName (name: string) : Result<string, string> =
    name
    |> Check.String.present
    |> Result.mapError (fun _ -> "name is required")

let loadUser : Flow<RequestEnv, string, User> =
    flow {
        let! db = Flow.read _.Db // Flow<RequestEnv, string, AppDb>
        let! user = db.FindUser 42 |> Flow.fromOption "user not found" // Flow<RequestEnv, string, User>
        return user
    }

let renderTrace : Flow<RequestEnv, string, string> =
    flow {
        let! env = Flow.env // Flow<RequestEnv, string, RequestEnv>
        let! user = loadUser // Flow<RequestEnv, string, User>
        let! validName = validateName user.Name // Flow<RequestEnv, string, string>
        return $"{env.Prefix} [{env.TraceId}] {validName}"
    }

let publishResponse : Flow<RequestEnv, string, string> =
    flow {
        let! env = Flow.env // Flow<RequestEnv, string, RequestEnv>
        let! user = loadUser // Flow<RequestEnv, string, User>
        let! suffix = env.LoadSuffix // Flow<RequestEnv, string, string>
        return $"{env.Prefix} [{env.TraceId}] {user.Name}{suffix}"
    }

let run () =
    let environment =
        { TraceId = Guid.Parse "11111111-1111-1111-1111-111111111111"
          Prefix = "Hello"
          Db =
            { FindUser =
                function
                | 42 -> Some { Id = 42; Name = "Ada" }
                | _ -> None }
          LoadSuffix = Task.FromResult "!" }

    let syncResult =
        loadUser
        |> fun workflow -> workflow.RunSynchronously(environment)

    let asyncResult =
        renderTrace
        |> fun workflow -> workflow.RunSynchronously(environment)

    let taskResult =
        publishResponse
        |> fun workflow -> workflow.RunSynchronously(environment)

    printfn "Flow result: %A" syncResult
    printfn "Flow result: %A" asyncResult
    printfn "Flow result: %A" taskResult
    // Flow result: Ok { Id = 42; Name = "Ada" }
    // Flow result: Ok "Hello [11111111-1111-1111-1111-111111111111] Ada"
    // Flow result: Ok "Hello [11111111-1111-1111-1111-111111111111] Ada!"

```

## Playground Example

This example shows the same core boundary across Flow using the normal direct-bind style inside each computation expression.

Run it:

```bash
dotnet run --project examples/Axial.Playground/Axial.Playground.fsproj --nologo
```

Source:

- [Program.fs](https://github.com/adz/Axial/blob/main/examples/Axial.Playground/Program.fs)

Source code:

```fsharp
open System
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.ErrorHandling
open Axial.Validation

type AppEnv =
    { Prefix: string
      Name: string
      LoadSuffix: Task<string> }

let greetingFlow : Flow<AppEnv, string, string> =
    Flow.read (fun env -> $"{env.Prefix} {env.Name}") // Flow<AppEnv, string, string>

let greetingAsync : Flow<AppEnv, string, string> =
    flow {
        let! greeting = greetingFlow
        let! checkedGreeting =
            greeting
            |> Check.String.present
            |> Result.mapError (fun _ -> "Blanko")

        return checkedGreeting.ToUpperInvariant()
    }

let greetingTask : Flow<AppEnv, string, string> =
    flow {
        let! env = Flow.env // Flow<AppEnv, string, AppEnv>
        let! greeting = greetingFlow // Flow<AppEnv, string, string>
        let! suffix = env.LoadSuffix // Flow<AppEnv, string, string>
        return $"{greeting}{suffix}"
    }

[<EntryPoint>]
let main _ =
    let env =
        { Prefix = "Hello"
          Name = "Ada"
          LoadSuffix = Task.FromResult "!" }

    let syncResult =
        greetingFlow
        |> fun workflow -> workflow.RunSynchronously(env)

    let asyncResult =
        greetingAsync
        |> fun workflow -> workflow.RunSynchronously(env)

    let taskResult =
        greetingTask
        |> fun workflow -> workflow.RunSynchronously(env)

    printfn "Flow: %A" syncResult
    printfn "Async: %A" asyncResult
    printfn "Task: %A" taskResult
    // Flow: Ok "Hello Ada"
    // Async: Ok "HELLO ADA"
    // Task: Ok "Hello Ada!"
    0

```

