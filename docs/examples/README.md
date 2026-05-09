---
title: Runnable Examples
description: Application-shaped examples that are executed during docs generation and mirrored back into the site.
---

# Runnable Examples

This page shows the examples that are executed during the docs build, so the public docs stay tied to real code and observed output.

The examples below are built from the repository projects, run with the current source, and then written back into this page.

The code blocks keep the important API calls on the same lines as the values they bind, with trailing comments where that makes the signature easier to read.
The examples prefer the normal direct-bind style inside computation expressions, so the docs reflect the recommended day-to-day usage.

## Request Boundary Example

This example shows a request boundary that pulls a user from a database-like environment, threads a trace id through the request context, and reuses the same validation shape across Flow, AsyncFlow, and TaskFlow.

Run it:

```bash
FSFLOW_EXAMPLE=request-boundary dotnet run --project examples/FsFlow.Examples/FsFlow.Examples.fsproj --nologo
```

Source:

- [RequestBoundaryExample.fs](https://github.com/adz/FsFlow/blob/main/examples/FsFlow.Examples/RequestBoundaryExample.fs)

Source code:

```fsharp
module RequestBoundaryExample

open System
open System.Threading
open System.Threading.Tasks
open FsFlow

type User =
    { Id: int
      Name: string }

type AppDb =
    { FindUser: int -> User option }

type RequestEnv =
    { TraceId: Guid
      Prefix: string
      Db: AppDb
      LoadSuffix: ColdTask<string> }

let validateName (name: string) : Result<string, string> =
    Check.notBlank name
    |> Check.orError "name is required"

let loadUser : Flow<RequestEnv, string, User> =
    flow {
        let! db = Flow.read _.Db // Flow<RequestEnv, string, AppDb>
        let! user = db.FindUser 42 |> Flow.fromOption "user not found" // Flow<RequestEnv, string, User>
        return user
    }

let renderTrace : AsyncFlow<RequestEnv, string, string> =
    asyncFlow {
        let! env = AsyncFlow.env // AsyncFlow<RequestEnv, string, RequestEnv>
        let! user = loadUser // AsyncFlow<RequestEnv, string, User>
        let! validName = validateName user.Name // AsyncFlow<RequestEnv, string, string>
        return $"{env.Prefix} [{env.TraceId}] {validName}"
    }

let publishResponse : TaskFlow<RequestEnv, string, string> =
    taskFlow {
        let! env = TaskFlow.env // TaskFlow<RequestEnv, string, RequestEnv>
        let! user = loadUser // TaskFlow<RequestEnv, string, User>
        let! suffix = env.LoadSuffix // TaskFlow<RequestEnv, string, string>
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
          LoadSuffix = ColdTask(fun _ -> Task.FromResult "!") }

    let syncResult =
        loadUser
        |> Flow.run environment

    let asyncResult =
        renderTrace
        |> AsyncFlow.run environment
        |> Async.RunSynchronously

    let taskResult =
        publishResponse
        |> TaskFlow.run environment CancellationToken.None
        |> fun task -> task.GetAwaiter().GetResult()

    printfn "Flow result: %A" syncResult
    printfn "AsyncFlow result: %A" asyncResult
    printfn "TaskFlow result: %A" taskResult
    // Flow result: Ok { Id = 42; Name = "Ada" }
    // AsyncFlow result: Ok "Hello [11111111-1111-1111-1111-111111111111] Ada"
    // TaskFlow result: Ok "Hello [11111111-1111-1111-1111-111111111111] Ada!"

```

## Diagnostics Example

This example shows a JSON-shaped request boundary with a root-level error, nested child branches, and a display-friendly diagnostics tree.

Run it:

```bash
FSFLOW_EXAMPLE=diagnostics dotnet run --project examples/FsFlow.Examples/FsFlow.Examples.fsproj --nologo
```

Source:

- [DiagnosticsExample.fs](https://github.com/adz/FsFlow/blob/main/examples/FsFlow.Examples/DiagnosticsExample.fs)

Source code:

```fsharp
module DiagnosticsExample

open System.Text.Json
open FsFlow

type CustomerLine =
    { Name: string }

type CustomerAddress =
    { City: string }

type Customer =
    { Name: string
      Address: CustomerAddress
      Lines: CustomerLine list }

type CreateCustomerRequest =
    { RequestId: string
      Customer: Customer }

type ApiError =
    { path: string
      message: string }

type ApiErrorResponse =
    { errors: ApiError list }

let jsonOptions = JsonSerializerOptions(WriteIndented = true)

let validateAddress address =
    validate.key "address" {
        let! city =
            validate.name "City" {
                return! address.City |> Check.notBlank |> Check.orError "City required"
            }

        return { address with City = city }
    }

let validateCustomer customer =
    validate {
        let! name =
            validate.name "Name" {
                return! customer.Name |> Check.notBlank |> Check.orError "Name required"
            }

        and! address = validateAddress customer.Address

        and! lines =
            validate.key "lines" {
                return!
                    customer.Lines
                    |> Validation.traverseIndexed (fun index line ->
                        validate.name "Name" {
                            let! name =
                                line.Name |> Check.notBlank |> Check.orError $"Line {index} name required"

                            return { Name = name }
                        }
                    )
            }

        return
            { customer with
                Name = name
                Address = address
                Lines = lines }
    }

let renderPath (path: PathSegment list) =
    path
    |> List.map (function
        | PathSegment.Key value
        | PathSegment.Name value -> value
        | PathSegment.Index index -> $"[{index}]")
    |> String.concat "."

let toApiErrors (graph: Diagnostics<'error>) =
    { errors =
        graph
        |> Diagnostics.flatten
        |> List.map (fun diagnostic ->
            { path = renderPath diagnostic.Path
              message = string diagnostic.Error }) }

let validateCreateCustomerRequest request =
    validate {
        let! requestId =
            validate.name "RequestId" {
                return! request.RequestId |> Check.notBlank |> Check.orError "RequestId required"
            }

        and! customer =
            validate.key "customer" {
                return! validateCustomer request.Customer
            }

        return { request with RequestId = requestId; Customer = customer }
    }

let run () =
    let requestJson =
        """{
  "requestId": "",
  "customer": {
    "name": "",
    "address": { "city": "" },
    "lines": [ { "name": "" } ]
  }
}"""

    let badRequest =
        { RequestId = ""
          Customer =
            { Name = ""
              Address = { City = "" }
              Lines = [ { Name = "" } ] } }

    let diagnosticsText =
        validateCreateCustomerRequest badRequest
        |> Validation.toResult
        |> Result.mapError (toApiErrors >> fun payload -> JsonSerializer.Serialize(payload, jsonOptions))
        |> function
            | Ok _ -> "Ok"
            | Error text -> text

    printfn "Request JSON:\n%s" requestJson
    printfn "API error JSON:\n%s" diagnosticsText
    // Request JSON:
    // {
    //   "requestId": "",
    //   "customer": {
    //     "name": "",
    //     "address": { "city": "" },
    //     "lines": [ { "name": "" } ]
    //   }
    // }
    // API error JSON:
    // {
    //   "errors": [
    //     {
    //       "path": "customer.address.City",
    //       "message": "City required"
    //     },
    //     {
    //       "path": "customer.lines.[0].Name",
    //       "message": "Line 0 name required"
    //     },
    //     {
    //       "path": "customer.Name",
    //       "message": "Name required"
    //     },
    //     {
    //       "path": "RequestId",
    //       "message": "RequestId required"
    //     }
    //   ]
    // }

```

## CAPS Core Example

This example shows the sync-first FsFlow.Caps.Core surface: fixed and live capability providers, plus typed errors for missing and invalid environment variables.

Run it:

```bash
dotnet run --project examples/FsFlow.Caps.Core.Examples/FsFlow.Caps.Core.Examples.fsproj --nologo
```

Source:

- [CoreCapabilitiesExample.fs](https://github.com/adz/FsFlow/blob/main/examples/FsFlow.Caps.Core.Examples/CoreCapabilitiesExample.fs)

Source code:

```fsharp
namespace FsFlow.Caps.Core.Examples

open System
open FsFlow.Caps.Core

module CoreCapabilitiesExample =
    let private renderResult formatter result =
        match result with
        | Ok value -> $"Ok {formatter value}"
        | Error error -> $"Error {EnvironmentVariableErrors.describe error}"

    let run () =
        let clock = Clock.fromValue (DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero))
        let random = Random.fromValue 7
        let guid = Guid.fromValue (global.System.Guid.Parse "11111111-1111-1111-1111-111111111111")

        let environment =
            EnvironmentVariables.fromPairs
                [ "FSFLOW_CAPS_PORT", "8080"
                  "FSFLOW_CAPS_ENABLED", "true"
                  "FSFLOW_CAPS_SESSION", "22222222-2222-2222-2222-222222222222"
                  "FSFLOW_CAPS_PORT_TEXT", "abc" ]

        printfn "clock=%O" (Clock.now clock)
        printfn "random=%d" (Random.nextInt random 0 10)
        printfn "guid=%O" (Guid.newGuid guid)
        printfn "port=%s" (renderResult string (EnvironmentVariable.getInt environment "FSFLOW_CAPS_PORT"))
        printfn "enabled=%s" (renderResult string (EnvironmentVariable.getBool environment "FSFLOW_CAPS_ENABLED"))
        printfn "session=%s" (renderResult string (EnvironmentVariable.getGuid environment "FSFLOW_CAPS_SESSION"))
        printfn "missing=%s" (renderResult string (EnvironmentVariable.get environment "FSFLOW_CAPS_MISSING"))
        printfn "invalid=%s" (renderResult string (EnvironmentVariable.getInt environment "FSFLOW_CAPS_PORT_TEXT"))

```

## Playground Example

This example shows the same core boundary across Flow, AsyncFlow, and TaskFlow using the normal direct-bind style inside each computation expression.

Run it:

```bash
dotnet run --project examples/FsFlow.Playground/FsFlow.Playground.fsproj --nologo
```

Source:

- [Program.fs](https://github.com/adz/FsFlow/blob/main/examples/FsFlow.Playground/Program.fs)

Source code:

```fsharp
open System
open System.Threading
open System.Threading.Tasks
open FsFlow

type AppEnv =
    { Prefix: string
      Name: string
      LoadSuffix: ColdTask<string> }

let greetingFlow : Flow<AppEnv, string, string> =
    Flow.read (fun env -> $"{env.Prefix} {env.Name}") // Flow<AppEnv, string, string>

let greetingAsyncFlow : AsyncFlow<AppEnv, string, string> =
    asyncFlow {
        let! greeting = greetingFlow // AsyncFlow<AppEnv, string, string>
        return greeting.ToUpperInvariant()
    }

let greetingTaskFlow : TaskFlow<AppEnv, string, string> =
    taskFlow {
        let! env = TaskFlow.env // TaskFlow<AppEnv, string, AppEnv>
        let! greeting = greetingFlow // TaskFlow<AppEnv, string, string>
        let! suffix = env.LoadSuffix // TaskFlow<AppEnv, string, string>
        return $"{greeting}{suffix}"
    }

[<EntryPoint>]
let main _ =
    let env =
        { Prefix = "Hello"
          Name = "Ada"
          LoadSuffix = ColdTask(fun _ -> Task.FromResult "!") }

    let syncResult =
        greetingFlow
        |> Flow.run env

    let asyncResult =
        greetingAsyncFlow
        |> AsyncFlow.run env
        |> Async.RunSynchronously

    let taskResult =
        greetingTaskFlow
        |> TaskFlow.run env CancellationToken.None
        |> fun task -> task.GetAwaiter().GetResult()

    printfn "Flow: %A" syncResult
    printfn "AsyncFlow: %A" asyncResult
    printfn "TaskFlow: %A" taskResult
    // Flow: Ok "Hello Ada"
    // AsyncFlow: Ok "HELLO ADA"
    // TaskFlow: Ok "Hello Ada!"
    0

```

## Maintenance Example

This example shows smaller, focused shapes for maintenance and interop scenarios without switching away from the normal direct-bind style.

Run it:

```bash
dotnet run --project examples/FsFlow.MaintenanceExamples/FsFlow.MaintenanceExamples.fsproj --nologo
```

Source:

- [Program.fs](https://github.com/adz/FsFlow/blob/main/examples/FsFlow.MaintenanceExamples/Program.fs)

Source code:

```fsharp
open System
open System.Threading
open System.Threading.Tasks
open FsFlow

let runFlow label env workflow =
    let result = Flow.run env workflow
    printfn "%s: %A" label result

let runAsyncFlow label env workflow =
    let result =
        workflow
        |> AsyncFlow.run env
        |> Async.RunSynchronously

    printfn "%s: %A" label result

let runTaskFlow label env workflow =
    let result =
        workflow
        |> TaskFlow.run env CancellationToken.None
        |> fun task -> task.GetAwaiter().GetResult()

    printfn "%s: %A" label result

let syncExample : Flow<int, string, int> =
    Flow.read id // Flow<int, string, int>
    |> Flow.map ((+) 1)

let asyncExample : AsyncFlow<int, string, int> =
    asyncFlow {
        let! value = syncExample // AsyncFlow<int, string, int>
        return value * 2
    }

let taskExample : TaskFlow<int, string, int> =
    taskFlow {
        let! env = TaskFlow.env // TaskFlow<int, string, int>
        let! suffix = ColdTask(fun _ -> Task.FromResult 5) // TaskFlow<int, string, int>
        return env + suffix
    }

[<EntryPoint>]
let main _ =
    runFlow "Flow" 20 syncExample
    runAsyncFlow "AsyncFlow" 20 asyncExample
    runTaskFlow "TaskFlow" 20 taskExample
    // Flow: Ok 21
    // AsyncFlow: Ok 42
    // TaskFlow: Ok 25
    0

```

