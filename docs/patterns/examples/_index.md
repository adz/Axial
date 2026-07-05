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

    let create value : Result<ContactEmail, RefinementError> =
        Refine.withChecks
            "ContactEmail"
            [ Check.String.present; Check.String.email; Check.String.maxLength 254 ]
            ContactEmail
            value

module Sku =
    let value (Sku value) = value

    let create value : Result<Sku, RefinementError> =
        Refine.withChecks
            "Sku"
            [ Check.String.present; Check.String.lengthBetween 3 12; Check.String.matches "^[A-Z0-9-]+$" ]
            Sku
            value

module Rating =
    let value (Rating value) = value

    let create value : Result<Rating, RefinementError> =
        Refine.withCheck "Rating" (Check.Number.between 1 5) Rating value

module UnitPrice =
    let value (UnitPrice value) = value

    let create value : Result<UnitPrice, RefinementError> =
        Refine.withCheck "UnitPrice" (Check.Number.greaterThan 0m) UnitPrice value

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
        (RefinementError.CheckFailed("Discount", [ CheckFailure.InvalidFormat "positive integer percent or slug code" ]))
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

## Refined Value Schema Example

This example shows schema-level refined values (Email, ContactName, a positive Quantity, and a non-negative Balance) built with Value.refined, composed into a record schema, and checked with ValueSchemaCheck.

Run it:

```bash
AXIAL_EXAMPLE=refined-value-schema dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo
```

Source:

- [RefinedValueSchemaExample.fs](https://github.com/adz/Axial/blob/main/examples/Axial.Examples/RefinedValueSchemaExample.fs)

Source code:

```fsharp
module RefinedValueSchemaExample

open Axial.Schema
open Axial.Validation.Schema

/// <summary>An email address refined over Axial's text primitive, carrying the well-known email format.</summary>
type Email = private Email of string

module Email =
    let create (value: string) = Email value
    let value (Email value) = value

    let schema : ValueSchema<Email> =
        Value.text
        |> Value.withConstraint SchemaConstraint.required
        |> Value.refined create value
        |> Value.withConstraint SchemaConstraint.email
        |> Value.withFormat SchemaFormat.email

/// <summary>A bounded-text domain value whose length constraints live on the raw text schema.</summary>
type ContactName = private ContactName of string

module ContactName =
    let create (value: string) = ContactName value
    let value (ContactName value) = value

    let schema : ValueSchema<ContactName> =
        Value.text
        |> Value.withConstraints [ SchemaConstraint.minLength 2; SchemaConstraint.maxLength 40 ]
        |> Value.refined create value

/// <summary>A quantity that must always be positive (strictly greater than zero).</summary>
type Quantity = private Quantity of int

module Quantity =
    let create (value: int) = Quantity value
    let value (Quantity value) = value

    let schema : ValueSchema<Quantity> =
        Value.int
        |> Value.withConstraint (SchemaConstraint.greaterThan 0)
        |> Value.refined create value

/// <summary>A running total that must never go negative, but zero is allowed.</summary>
type Balance = private Balance of decimal

module Balance =
    let create (value: decimal) = Balance value
    let value (Balance value) = value

    let schema : ValueSchema<Balance> =
        Value.decimal
        |> Value.withConstraint (SchemaConstraint.atLeast 0m)
        |> Value.refined create value

type Contact =
    { Email: Email
      Name: ContactName
      Quantity: Quantity
      Balance: Balance }

let contactSchema =
    Schema.recordFor<Contact, _> (fun email name quantity balance ->
        { Email = email
          Name = name
          Quantity = quantity
          Balance = balance })
    |> Schema.field "email" _.Email Email.schema
    |> Schema.field "name" _.Name ContactName.schema
    |> Schema.field "quantity" _.Quantity Quantity.schema
    |> Schema.field "balance" _.Balance Balance.schema
    |> Schema.build

let run () =
    let contact =
        { Email = Email.create "ada@example.com"
          Name = ContactName.create "Ada"
          Quantity = Quantity.create 3
          Balance = Balance.create 0m }

    let emailCheck = Email.schema |> ValueSchemaCheck.text
    let nameCheck = ContactName.schema |> ValueSchemaCheck.text
    let quantityCheck = Quantity.schema |> ValueSchemaCheck.ordered<int, _>
    let balanceCheck = Balance.schema |> ValueSchemaCheck.ordered<decimal, _>

    printfn "Email check: %A" (emailCheck contact.Email)
    printfn "Name check: %A" (nameCheck contact.Name)
    printfn "Quantity check: %A" (quantityCheck contact.Quantity)
    printfn "Balance check: %A" (balanceCheck contact.Balance)

    printfn "Invalid email check: %A" (emailCheck (Email.create ""))
    printfn "Invalid name check: %A" (nameCheck (ContactName.create "A"))
    printfn "Invalid quantity check: %A" (quantityCheck (Quantity.create 0))
    printfn "Invalid balance check: %A" (balanceCheck (Balance.create -1m))

```

## Minimal API Boundary Example

This example is a complete ASP.NET Core minimal API where one schema declaration drives JSON body parsing with 400 path diagnostics, trusted-model serialization through the compiled codec, a generated OpenAPI document, and an HTML form with redisplay. Running it with AXIAL_EXAMPLE=smoke starts the server and exercises every endpoint.

Run it:

```bash
AXIAL_EXAMPLE=smoke dotnet run --project examples/Axial.Api/Axial.Api.fsproj --nologo
```

Source:

- [Program.fs](https://github.com/adz/Axial/blob/main/examples/Axial.Api/Program.fs)

Source code:

```fsharp
/// A complete ASP.NET Core minimal API where one schema declaration drives everything at the boundary:
///
/// - `POST /signups` parses the JSON body through the schema: bad input gets a 400 with path diagnostics,
///   good input becomes a trusted model that is echoed back through the compiled JSON codec.
/// - `GET /openapi.json` serves an OpenAPI document whose request schema is generated from the same declaration.
/// - `GET /signup` renders an HTML form from the schema's inspection metadata, and `POST /signup` redisplays the
///   submitted values next to their errors.
///
/// Run it directly (`dotnet run --project examples/Axial.Api/Axial.Api.fsproj`) and browse to /signup, or run the
/// self-contained smoke pass with `AXIAL_EXAMPLE=smoke`, which is what CI and the docs build execute.
module Axial.Api.Program

open System
open System.Net.Http
open System.Text
open System.Text.Json
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Axial.Schema
open Axial.Codec
open Axial.Validation.Schema

// ---------------------------------------------------------------------------
// Domain model: parse, don't validate. Email can only be constructed by the
// schema (or its own tryParse), so a Signup in hand is always trusted.
// ---------------------------------------------------------------------------

type Email = private EmailValue of string

module Email =
    let value (EmailValue raw) = raw

    let schema: ValueSchema<Email> =
        Value.text
        |> Value.withConstraints
            [ SchemaConstraint.required
              SchemaConstraint.maxLength 254
              SchemaConstraint.email ]
        |> Value.refined EmailValue value
        |> Value.withFormat SchemaFormat.email

type Address = { Street: string; City: string }

type Signup =
    { Name: string
      Email: Email
      Age: int
      Address: Address
      Tags: string list }

module Signup =
    let addressSchema =
        Schema.recordFor<Address, _> (fun street city -> { Street = street; City = city })
        |> Schema.fieldWith [ SchemaConstraint.required; SchemaConstraint.maxLength 120 ] "street" _.Street Value.text
        |> Schema.fieldWith [ SchemaConstraint.required; SchemaConstraint.maxLength 80 ] "city" _.City Value.text
        |> Schema.build

    let schema =
        Schema.recordFor<Signup, _> (fun name email age address tags ->
            { Name = name
              Email = email
              Age = age
              Address = address
              Tags = tags })
        |> Schema.fieldWith [ SchemaConstraint.required; SchemaConstraint.maxLength 80 ] "name" _.Name Value.text
        |> Schema.field "email" _.Email Email.schema
        |> Schema.fieldWith [ SchemaConstraint.between 13 120 ] "age" _.Age Value.int
        |> Schema.fieldWith [ SchemaConstraint.required ] "address" _.Address (Value.nested addressSchema)
        |> Schema.fieldWith [ SchemaConstraint.maxCount 5 ] "tags" _.Tags (Value.manyOf Value.text)
        |> Schema.build

// ---------------------------------------------------------------------------
// Interpreters compiled once from the declaration above.
// ---------------------------------------------------------------------------

module Boundary =
    let codec = Json.compile Signup.schema

    let jsonSchema = JsonSchema.generate Signup.schema

    let openApiDocument =
        // The request body schema is generated straight from the declaration, so
        // the published contract can never drift from what the parser accepts.
        sprintf
            """{"openapi":"3.1.0","info":{"title":"Axial signup sample","version":"1.0.0"},"paths":{"/signups":{"post":{"summary":"Create a signup","requestBody":{"required":true,"content":{"application/json":{"schema":%s}}},"responses":{"201":{"description":"The trusted signup that was parsed.","content":{"application/json":{"schema":%s}}},"400":{"description":"Path-aware parse diagnostics."}}}}}}"""
            jsonSchema
            jsonSchema

    /// Renders failed parse diagnostics as a JSON body of { path, message } entries.
    let errorBody (parsed: ParsedInput<Signup, SchemaError>) =
        let errors =
            parsed.Errors
            |> List.map (fun diagnostic ->
                let path =
                    diagnostic.Path
                    |> List.map (function
                        | Axial.Validation.PathSegment.Index index -> $"[{index}]"
                        | Axial.Validation.PathSegment.Key key -> key
                        | Axial.Validation.PathSegment.Name name -> name)
                    |> String.concat "."

                {| path = path
                   message = SchemaError.render diagnostic.Error |})

        {| errors = errors |}

// ---------------------------------------------------------------------------
// A small HTML form renderer over the schema's inspection metadata.
// ---------------------------------------------------------------------------

module FormPage =
    let private encode (text: string) = System.Net.WebUtility.HtmlEncode text

    let private constraintAttributes (field: FieldDescription) =
        let metadata =
            (field.Constraints |> List.map _.Metadata)
            @ (let rec gather (value: ValueDescription) =
                (value.Constraints |> List.map _.Metadata)
                @ (match value.Shape with
                   | ValueShape.Refined underlying -> gather underlying
                   | _ -> [])

               gather field.Value)

        let required =
            if metadata |> List.contains SchemaConstraintMetadata.Required then " required" else ""

        let maxLength =
            metadata
            |> List.tryPick (function
                | SchemaConstraintMetadata.MaxLength maximum -> Some $" maxlength=\"{maximum}\""
                | _ -> None)
            |> Option.defaultValue ""

        required + maxLength

    let private inputType (field: FieldDescription) =
        let rec shape (value: ValueDescription) =
            match value.Shape with
            | ValueShape.Refined underlying -> shape underlying
            | other -> other

        match field.Value.Format, shape field.Value with
        | Some format, _ when format = SchemaFormat.email -> "email"
        | _, ValueShape.Primitive PrimitiveValueKind.Int -> "number"
        | _ -> "text"

    /// Renders one flat form from the schema description, redisplaying raw input and attaching errors by path.
    let render (parsed: ParsedInput<Signup, SchemaError> option) =
        let input =
            parsed |> Option.map _.Input |> Option.defaultValue (RawInput.Object Map.empty)

        let errorsFor path =
            match parsed with
            | None -> []
            | Some parsed -> parsed.ErrorsFor(path: string) |> List.map SchemaError.render

        let fieldRow prefix (field: FieldDescription) =
            let path = if prefix = "" then field.Name else $"{prefix}.{field.Name}"
            let value = RawInput.redisplayPath path input

            let errors =
                errorsFor path
                |> List.map (fun message -> $"""<p class="error">{encode message}</p>""")
                |> String.concat ""

            $"""<label for="{path}">{encode field.Name}</label>
<input type="{inputType field}" id="{path}" name="{path}" value="{encode value}"{constraintAttributes field} />
{errors}"""

        let description = Inspect.model Signup.schema

        let rows =
            description.Fields
            |> List.collect (fun field ->
                match field.Value.Shape with
                | ValueShape.Nested nested -> nested.Fields |> List.map (fieldRow field.Name)
                | ValueShape.Many _ -> [ fieldRow "" field ]
                | _ -> [ fieldRow "" field ])
            |> String.concat "\n"

        let banner =
            match parsed with
            | Some parsed when not parsed.IsValid ->
                """<p class="error">Please fix the errors below and resubmit.</p>"""
            | Some _ -> """<p class="success">Signup accepted.</p>"""
            | None -> ""

        $"""<!doctype html>
<html><head><title>Signup</title><style>
body {{ font-family: system-ui, sans-serif; max-width: 32rem; margin: 2rem auto; }}
label {{ display: block; margin-top: 1rem; }}
input {{ width: 100%%; padding: 0.4rem; }}
.error {{ color: #b00020; margin: 0.2rem 0 0; }}
.success {{ color: #1b7a2f; }}
</style></head><body>
<h1>Signup</h1>
{banner}
<form method="post" action="/signup">
{rows}
<button type="submit" style="margin-top:1.5rem">Sign up</button>
</form>
</body></html>"""

// ---------------------------------------------------------------------------
// The minimal API host.
// ---------------------------------------------------------------------------

let private formToRawInput (form: IFormCollection) =
    // Dotted form field names such as address.street become nested raw input
    // through the configuration-style path builder.
    form
    |> Seq.collect (fun pair -> pair.Value |> Seq.map (fun value -> pair.Key.Replace(".", ":"), value))
    |> RawInput.ofConfiguration

let buildApp (args: string[]) =
    let builder = WebApplication.CreateBuilder(args)
    builder.Logging.ClearProviders() |> ignore
    let app = builder.Build()

    app.MapPost(
        "/signups",
        Func<HttpRequest, System.Threading.Tasks.Task<IResult>>(fun request ->
            task {
                use! document = JsonDocument.ParseAsync request.Body
                let parsed = Input.parse Signup.schema (RawInput.ofJsonDocument document)

                match parsed.Result with
                | Ok signup ->
                    // The trusted model round-trips through the compiled codec, proving
                    // the same declaration drives serialization too.
                    return Results.Text(Json.serialize Boundary.codec signup, "application/json", statusCode = 201)
                | Error _ -> return Results.Json(Boundary.errorBody parsed, statusCode = 400)
            })
    )
    |> ignore

    app.MapGet(
        "/openapi.json",
        Func<IResult>(fun () -> Results.Text(Boundary.openApiDocument, "application/json"))
    )
    |> ignore

    app.MapGet("/signup", Func<IResult>(fun () -> Results.Text(FormPage.render None, "text/html")))
    |> ignore

    app.MapPost(
        "/signup",
        Func<HttpRequest, System.Threading.Tasks.Task<IResult>>(fun request ->
            task {
                let! form = request.ReadFormAsync()
                let parsed = Input.parse Signup.schema (formToRawInput form)
                return Results.Text(FormPage.render (Some parsed), "text/html")
            })
    )
    |> ignore

    app

/// Starts the app on an ephemeral port, exercises every endpoint, and prints what one schema declaration produced.
let private runSmokeTest () =
    task {
        let app = buildApp [| "--urls"; "http://127.0.0.1:0" |]
        do! app.StartAsync()

        let address =
            app.Urls |> Seq.head

        use client = new HttpClient(BaseAddress = Uri address)

        let post (path: string) (body: string) (contentType: string) =
            task {
                use content = new StringContent(body, Encoding.UTF8, contentType)
                let! response = client.PostAsync(path, content)
                let! text = response.Content.ReadAsStringAsync()
                return int response.StatusCode, text
            }

        let validBody =
            """{"name":"Ada Lovelace","email":"ada@example.com","age":36,"address":{"street":"12 Analytical Way","city":"London"},"tags":["vip"]}"""

        let invalidBody =
            """{"name":"","email":"not-an-email","age":9,"address":{"street":"12 Analytical Way"},"tags":["a","b","c","d","e","f"]}"""

        let! validStatus, validText = post "/signups" validBody "application/json"
        printfn "POST /signups (valid) -> %d" validStatus
        printfn "%s" validText

        let! invalidStatus, invalidText = post "/signups" invalidBody "application/json"
        printfn "POST /signups (invalid) -> %d" invalidStatus
        printfn "%s" invalidText

        let! openApi = client.GetStringAsync "/openapi.json"
        printfn "GET /openapi.json contains generated schema: %b" (openApi.Contains "\"maxLength\":254")

        let! formHtml = client.GetStringAsync "/signup"
        printfn "GET /signup renders schema-driven form fields: %b" (formHtml.Contains "name=\"address.street\"")

        let! formStatus, formText =
            post "/signup" "name=&email=ada%40example.com&age=12&address.street=12+Analytical+Way&address.city=London" "application/x-www-form-urlencoded"

        printfn "POST /signup (form redisplay) -> %d shows errors: %b" formStatus (formText.Contains "class=\"error\"")
        printfn "POST /signup redisplays submitted street: %b" (formText.Contains "value=\"12 Analytical Way\"")

        do! app.StopAsync()
    }

[<EntryPoint>]
let main args =
    if Environment.GetEnvironmentVariable "AXIAL_EXAMPLE" = "smoke" || args |> Array.contains "smoke" then
        (runSmokeTest ()).GetAwaiter().GetResult()
        0
    else
        (buildApp args).Run()
        0

```

## Policy Example

This example shows Policy adapting every verification boundary — raw parsing, refined construction, schema input parsing, intrinsic validation, and contextual rules — into one workflow error type run with Flow.verify.

Run it:

```bash
AXIAL_EXAMPLE=policy dotnet run --project examples/Axial.Examples/Axial.Examples.fsproj --nologo
```

Source:

- [PolicyExamples.fs](https://github.com/adz/Axial/blob/main/examples/Axial.Examples/PolicyExamples.fs)

Source code:

```fsharp
/// Shows how Policy adapts each Axial verification boundary — raw parsing, refined
/// construction, schema input parsing, intrinsic validation, and contextual rules —
/// into one workflow error type that Flow.verify can run inside a flow.
module PolicyExamples

open Axial.Flow
open Axial.Refined
open Axial.Schema
open Axial.Validation
open Axial.Validation.Schema

type Quantity = private Quantity of int

module Quantity =
    let create (value: int) = Quantity value
    let value (Quantity value) = value

    let schema : ValueSchema<Quantity> =
        Value.int
        |> Value.withConstraint (SchemaConstraint.greaterThan 0)
        |> Value.refined create value

type OrderLine =
    { Sku: string
      Quantity: Quantity }

let orderLineSchema =
    Schema.recordFor<OrderLine, _> (fun sku quantity ->
        { Sku = sku
          Quantity = quantity })
    |> Schema.text "sku" _.Sku
    |> Schema.field "quantity" _.Quantity Quantity.schema
    |> Schema.build

type OrderEnv =
    { MaxLineQuantity: int
      EnforceQuantityCap: bool }

type OrderError =
    | QuantityNotANumber
    | QuantityNotPositive
    | LineRejected of Diagnostic<SchemaError> list
    | QuantityOverCap of int

// 1. Parsing: adapt a raw text parser, replacing its ParseError with a workflow error.
let parseQuantityText : Policy<OrderEnv, OrderError, string, int> =
    Policy.withError Parse.int QuantityNotANumber

// 2. Refined construction: adapt a refinement smart constructor.
let refinePositive : Policy<OrderEnv, OrderError, int, PositiveInt> =
    Policy.withError Refine.positiveInt QuantityNotPositive

// 3. Schema input result: adapt Input.parse over raw boundary input.
let parseOrderLine : Policy<OrderEnv, OrderError, RawInput, OrderLine> =
    Policy.lift
        (fun raw -> (Input.parse orderLineSchema raw).Result)
        (Diagnostics.flatten >> LineRejected)

// 4. Validation result: adapt intrinsic validation of an existing model.
let validateOrderLine : Policy<OrderEnv, OrderError, OrderLine, OrderLine> =
    Policy.lift
        (fun line ->
            Axial.Validation.Schema.Validation.validate orderLineSchema line
            |> Axial.Validation.Validation.toResult)
        (Diagnostics.flatten >> LineRejected)

// 5. Contextual rules: adapt a RuleSet evaluated with the workflow environment.
let quantityCapRules (env: OrderEnv) : RuleSet<OrderLine, OrderError> =
    Rules.create (fun line ->
        if Quantity.value line.Quantity > env.MaxLineQuantity then
            Rules.failAt [ PathSegment.Name "quantity" ] (QuantityOverCap env.MaxLineQuantity)
        else
            Ok ())

let underQuantityCap : Policy<OrderEnv, OrderError, OrderLine, OrderLine> =
    Policy.context
        (fun env line -> Rules.apply (quantityCapRules env) line)
        (Diagnostics.flatten >> List.map _.Error >> List.head)

// Policies over the same input/output compose, and environment predicates can
// switch a policy off without changing the workflow shape.
let acceptOrderLine : Policy<OrderEnv, OrderError, RawInput, OrderLine> =
    Policy.compose
        parseOrderLine
        (Policy.optional (fun env -> env.EnforceQuantityCap) underQuantityCap)

let acceptLine (raw: RawInput) : Flow<OrderEnv, OrderError, OrderLine> =
    flow {
        let! line = raw |> Flow.verify acceptOrderLine
        return line
    }

let run () =
    let env =
        { MaxLineQuantity = 10
          EnforceQuantityCap = true }

    let raw quantity =
        RawInput.Object(
            Map [ "sku", RawInput.Scalar "SKU-1"; "quantity", RawInput.Scalar quantity ])

    printfn "Policy examples"
    printfn "  parse text quantity: %A" (parseQuantityText env "3")
    printfn "  refine positive:     %A" (refinePositive env 3)
    printfn "  accepted line:       %A" (acceptLine (raw "3") |> fun f -> f.RunSynchronously(env))
    printfn "  rejected (not int):  %A" (acceptLine (raw "many") |> fun f -> f.RunSynchronously(env))
    printfn "  rejected (over cap): %A" (acceptLine (raw "50") |> fun f -> f.RunSynchronously(env))

    printfn
        "  cap disabled:        %A"
        (acceptLine (raw "50") |> fun f -> f.RunSynchronously({ env with EnforceQuantityCap = false }))

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

