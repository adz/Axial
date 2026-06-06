---
title: Runnable Examples
description: Application-shaped examples that are executed during docs generation and mirrored back into the site.
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
      LoadSuffix: Task<string> }

let validateName (name: string) : Result<string, string> =
    Take.whenNotBlank name
    |> Check.withError "name is required"

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
                return! address.City |> Take.whenNotBlank |> Check.withError "City required"
            }

        return { address with City = city }
    }

let validateCustomer customer =
    validate {
        let! name =
            validate.name "Name" {
                return! customer.Name |> Take.whenNotBlank |> Check.withError "Name required"
            }

        and! address = validateAddress customer.Address

        and! lines =
            validate.key "lines" {
                return!
                    customer.Lines
                    |> Validation.traverseIndexed (fun index line ->
                        validate.name "Name" {
                            let! name =
                                line.Name |> Take.whenNotBlank |> Check.withError $"Line {index} name required"

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
                return! request.RequestId |> Take.whenNotBlank |> Check.withError "RequestId required"
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

## Playground Example

This example shows the same core boundary across Flow using the normal direct-bind style inside each computation expression.

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
      LoadSuffix: Task<string> }

let greetingFlow : Flow<AppEnv, string, string> =
    Flow.read (fun env -> $"{env.Prefix} {env.Name}") // Flow<AppEnv, string, string>

let greetingAsync : Flow<AppEnv, string, string> =
    flow {
        let! greeting = greetingFlow
        return greeting.ToUpperInvariant()
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

