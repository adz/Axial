---
weight: 85
title: Runnable Examples
description: Executable workflow boundary examples mirrored back into the docs.
type: docs
---


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
    |> Check.present
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
        let! (checkedGreeting: string) =
            greeting
            |> Check.present
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

## Maintenance Example

This example shows smaller, focused shapes for maintenance and interop scenarios without switching away from the normal direct-bind style.

Run it:

```bash
dotnet run --project examples/Axial.MaintenanceExamples/Axial.MaintenanceExamples.fsproj --nologo
```

Source:

- [Program.fs](https://github.com/adz/Axial/blob/main/examples/Axial.MaintenanceExamples/Program.fs)

Source code:

```fsharp
open System
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.ErrorHandling
open Axial.Validation

let runFlow label env (workflow: Flow<'env, 'error, 'value>) =
    let result = workflow.RunSynchronously(env)
    printfn "%s: %A" label result

let runAsyncExample label env (workflow: Flow<'env, 'error, 'value>) =
    let result =
        workflow
        |> fun workflow -> workflow.RunSynchronously(env)

    printfn "%s: %A" label result

let runTaskExample label env (workflow: Flow<'env, 'error, 'value>) =
    let result =
        workflow
        |> fun workflow -> workflow.RunSynchronously(env)

    printfn "%s: %A" label result

let syncExample : Flow<int, string, int> =
    Flow.read id // Flow<int, string, int>
    |> Flow.map ((+) 1)

let asyncExample : Flow<int, string, int> =
    flow {
        let! value = async { return 21 }
        return value * 2
    }

let taskExample : Flow<int, string, int> =
    flow {
        let! env = Flow.read id
        let! suffix = Task.FromResult 5
        return env + suffix
    }

[<EntryPoint>]
let main _ =
    runFlow "Flow" 20 syncExample
    runAsyncExample "Async" 20 asyncExample
    runTaskExample "Task" 20 taskExample
    // Flow: Ok 21
    // Async: Ok 42
    // Task: Ok 25
    0

```

## Supervision and Fiber Observability Example

This example shows Flow.Runtime.supervise restarting a background worker that dies with a defect, a FiberObserver reporting the defect of a fiber whose fork handle was discarded, and Flow.forkDetached stating intentional fire-and-forget so the report is suppressed.

Run it:

```bash
AXIAL_EXAMPLE=supervision dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo
```

Source:

- [SupervisionExample.fs](https://github.com/adz/Axial/blob/main/examples/Axial.Examples/SupervisionExample.fs)

Source code:

```fsharp
module SupervisionExample

open System
open Axial.Flow

// Demonstrates defect supervision and fiber observability:
// 1. Flow.Runtime.supervise restarts background work that dies with a defect.
// 2. A FiberObserver installed once at the edge reports defects from fibers nobody awaited.
// 3. Flow.forkDetached states intentional fire-and-forget at the call site, silencing the report.

let private flakyWorker (attempts: int ref) : Flow<unit, string, string> =
    Flow.delay(fun () ->
        attempts.Value <- attempts.Value + 1

        if attempts.Value < 3 then
            // A bug, not a typed domain error: supervise restarts these.
            Flow.die (InvalidOperationException $"worker crashed on attempt {attempts.Value}")
        else
            Flow.succeed $"worker succeeded on attempt {attempts.Value}")

let private consoleObserver =
    { FiberObserver.none with
        OnEnd = fun metadata defect ->
            match defect with
            | Some exn -> printfn $"  [observer] fiber {metadata.Id.Value} died: {exn.Message}"
            | None -> printfn $"  [observer] fiber {metadata.Id.Value} ended: {metadata.Status}"
        OnUnobservedDefect = fun metadata defect ->
            let source =
                match metadata with
                | Some m -> $"fiber {m.Id.Value}"
                | None -> "race/timeout loser"

            printfn $"  [observer] UNOBSERVED DEFECT from {source}: {defect.Message}" }

let private supervisedRecovery () =
    printfn "-- Flow.Runtime.supervise: restart a background worker that dies with a defect"
    let attempts = ref 0

    let policy : SupervisePolicy =
        { MaxAttempts = 5
          Delay = fun _ -> TimeSpan.Zero
          ShouldRestart = fun _ -> true }

    let result =
        flakyWorker attempts
        |> Flow.Runtime.supervise policy
        |> fun workflow -> workflow.RunSynchronously(())

    printfn $"  result after {attempts.Value} attempts: %A{result}"

let private unobservedDefectReporting () =
    printfn "-- FiberObserver: a discarded fork handle whose fiber dies is reported"

    let workflow =
        flow {
            // The handle is deliberately discarded: without an observer this crash is silent.
            let! _fiber = Flow.fork (Flow.die (InvalidOperationException "background job blew up") : Flow<unit, string, int>)
            do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
            return "main workflow finished fine"
        }
        |> Flow.withFiberObserver consoleObserver

    let result = workflow.RunSynchronously(())
    printfn $"  result: %A{result}"

let private intentionalFireAndForget () =
    printfn "-- Flow.forkDetached: intentional fire-and-forget is not reported as unobserved"

    let workflow =
        flow {
            let! _fiber = Flow.forkDetached (Flow.die (InvalidOperationException "best-effort work failed") : Flow<unit, string, int>)
            do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 50.0)
            return "no unobserved-defect report for detached work"
        }
        |> Flow.withFiberObserver consoleObserver

    let result = workflow.RunSynchronously(())
    printfn $"  result: %A{result}"

let run () =
    printfn "=== Supervision and fiber observability ==="
    supervisedRecovery ()
    unobservedDefectReporting ()
    intentionalFireAndForget ()

```

