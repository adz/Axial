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
    |> Result.notBlank
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

## Diagnostics Example

This example shows a JSON-shaped request boundary with a root-level error, nested child branches, and a display-friendly diagnostics tree.

Run it:

```bash
AXIAL_EXAMPLE=diagnostics dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo
```

Source:

- [DiagnosticsExample.fs](https://github.com/adz/Axial/blob/main/examples/Axial.Examples/DiagnosticsExample.fs)

Source code:

```fsharp
module DiagnosticsExample

open System.Text.Json
open Axial.Flow
open Axial.ErrorHandling
open Axial.Validation

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

let private required message value =
    value
    |> Result.notBlank
    |> Result.mapError (fun _ -> message)

let validateAddressWithoutCEOrPipe address =
    Validation.at [PathSegment.Key "address"] (
        Validation.at [PathSegment.Name "City"] (
            Validation.fromResult (
                address.City |> required "City required"
            )
        )
        |> Validation.map (fun city -> {address with City = city })
    )

let validateAddressWithoutCE address =
    let cityResult =
        address.City
        |> required "City required"

    cityResult
    |> Validation.fromResult
    |> Validation.at [PathSegment.Name "City"]
    |> Validation.map (fun city -> {address with City = city })
    |> Validation.at [PathSegment.Key "address"]

// Equivalent using CE
let validateAddress address =
    validate.key "address" {
        let! city = validate.name "city" {
            return! address.City |> required "City required"
        }
        return { address with City = city }
    }

let validateCustomer customer =
    validate {
        let! name =
            validate.name "Name" {
                return! customer.Name |> required "Name required"
            }

        and! address = validateAddress customer.Address

        and! lines =
            validate.key "lines" {
                return!
                    customer.Lines
                    |> Validation.traverseIndexed (fun index line ->
                        validate.name "Name" {
                            let! name =
                                line.Name |> required $"Line {index} name required"

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
                return! request.RequestId |> required "RequestId required"
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

## Refined Catalog Example

This example shows a request boundary that parses strings, builds refined numeric/text/collection values, chooses a domain union case, and rejects invalid input before the domain record is created.

Run it:

```bash
AXIAL_EXAMPLE=refined-catalog dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo
```

Source:

- [RefinedCatalogExample.fs](https://github.com/adz/Axial/blob/main/examples/Axial.Examples/RefinedCatalogExample.fs)

Source code:

```fsharp
module RefinedCatalogExample

open System
open Axial.ErrorHandling
open Axial.Refined

type ProductId = ProductId of NonZeroInt
type ProductSlug = ProductSlug of Slug
type DisplayName = DisplayName of NonBlankString
type ProductTags = ProductTags of DistinctList<Slug>
type Quantity = Quantity of PositiveInt
type ContactEmail = private ContactEmail of string
type Sku = private Sku of string
type Rating = private Rating of int
type UnitPrice = private UnitPrice of decimal

module ContactEmail =
    let value (ContactEmail value) = value

    let private describe failures =
        failures
        |> List.map (sprintf "%A")
        |> String.concat "; "

    let create value : Result<ContactEmail, RefinementError> =
        Refine.withCheck
            "ContactEmail"
            (Check.all [
                Check.String.present
                Check.String.email
                Check.String.maxLength 254
            ])
            (fun target failures -> RefinementError.InvalidFormat(target, describe failures))
            ContactEmail
            value

module Sku =
    let value (Sku value) = value

    let private describe failures =
        failures
        |> List.map (sprintf "%A")
        |> String.concat "; "

    let create value : Result<Sku, RefinementError> =
        Refine.withCheck
            "Sku"
            (Check.all [
                Check.String.present
                Check.String.lengthBetween 3 12
                Check.String.matches "^[A-Z0-9-]+$"
            ])
            (fun target failures -> RefinementError.InvalidFormat(target, describe failures))
            Sku
            value

module Rating =
    let value (Rating value) = value

    let private describe failures =
        failures
        |> List.map (sprintf "%A")
        |> String.concat "; "

    let create value : Result<Rating, RefinementError> =
        Refine.withCheck
            "Rating"
            (Check.Number.between 1 5)
            (fun target failures -> RefinementError.OutOfRange(target, describe failures))
            Rating
            value

module UnitPrice =
    let value (UnitPrice value) = value

    let private describe failures =
        failures
        |> List.map (sprintf "%A")
        |> String.concat "; "

    let create value : Result<UnitPrice, RefinementError> =
        Refine.withCheck
            "UnitPrice"
            (Check.Number.greaterThan 0m)
            (fun target failures -> RefinementError.OutOfRange(target, describe failures))
            UnitPrice
            value

type Discount =
    | Percent of PositiveInt
    | Code of Slug

type PublishWindow =
    { Range: DateTimeOffsetRange }

type ProductRequest =
    { Id: ProductId
      Slug: ProductSlug
      DisplayName: DisplayName
      Tags: ProductTags
      Quantity: Quantity
      ContactEmail: ContactEmail
      Sku: Sku
      Rating: Rating
      UnitPrice: UnitPrice
      Discount: Discount
      PublishWindow: PublishWindow }

let sequenceResults values =
    let folder next state =
        match next, state with
        | Ok value, Ok values -> Ok(value :: values)
        | Error error, _ -> Error error
        | _, Error error -> Error error

    values
    |> List.foldBack folder
    <| Ok []

let parseDiscount (raw: string) : Result<Discount, RefinementError> =
    let parsePercent value =
        Parse.int value
        |> Result.mapError RefinementError.ParseFailed
        |> Result.bind Refine.positiveInt

    Choice.orElse
        Percent
        parsePercent
        Code
        Refine.slug
        (RefinementError.InvalidFormat("Discount", "Expected a positive integer percent or slug code."))
        raw

let createProductRequest
    rawId
    rawSlug
    rawDisplayName
    rawTags
    rawQuantity
    rawContactEmail
    rawSku
    rawRating
    rawUnitPrice
    rawDiscount
    publishStart
    publishEnd
    : Result<ProductRequest, RefinementError> =
    refine {
        let! parsedId = Parse.int rawId
        let! id = Refine.nonZeroInt parsedId
        let! slug = Refine.slug rawSlug
        let! displayName = Refine.nonBlankString rawDisplayName
        let! tags = rawTags |> List.map Refine.slug |> sequenceResults
        let! distinctTags = Refine.distinctList tags
        let! parsedQuantity = Parse.int rawQuantity
        let! quantity = Refine.positiveInt parsedQuantity
        let! contactEmail = ContactEmail.create rawContactEmail
        let! sku = Sku.create rawSku
        let! parsedRating = Parse.int rawRating
        let! rating = Rating.create parsedRating
        let! parsedUnitPrice = Parse.decimal rawUnitPrice
        let! unitPrice = UnitPrice.create parsedUnitPrice
        let! discount = parseDiscount rawDiscount
        let! publishWindow = Refine.dateTimeOffsetRange publishStart publishEnd

        return {
            Id = ProductId id
            Slug = ProductSlug slug
            DisplayName = DisplayName displayName
            Tags = ProductTags distinctTags
            Quantity = Quantity quantity
            ContactEmail = contactEmail
            Sku = sku
            Rating = rating
            UnitPrice = unitPrice
            Discount = discount
            PublishWindow = { Range = publishWindow }
        }
    }

let run () =
    let start = DateTimeOffset(2026, 6, 28, 9, 0, 0, TimeSpan.Zero)
    let finish = start.AddDays 7.0

    let valid =
        createProductRequest
            "42"
            "axial-guide"
            "Axial Guide"
            [ "fsharp"; "typed-errors" ]
            "3"
            "ada@example.com"
            "AX-42"
            "5"
            "19.95"
            "launch-sale"
            start
            finish

    let invalid =
        createProductRequest
            "0"
            "Bad Slug"
            " "
            [ "fsharp"; "fsharp" ]
            "-1"
            "not-email"
            "x"
            "6"
            "0"
            ""
            finish
            start

    printfn "Refined product result: %A" valid
    printfn "Refined product error: %A" invalid

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
            |> Result.notBlank
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

