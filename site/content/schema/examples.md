---
weight: 85
title: Runnable Examples
description: Executable schema, refined, diagnostics, and policy examples mirrored back into the docs.
type: docs
---


This page shows the examples that are executed during the docs build, so the public docs stay tied to real code and observed output.

The examples below are built from the repository projects, run with the current source, and then written back into this page.

The code blocks keep the important API calls on the same lines as the values they bind, with trailing comments where that makes the signature easier to read.
The examples prefer the normal direct-bind style inside computation expressions, so the docs reflect the recommended day-to-day usage.

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
    |> Check.present
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
open Axial.ErrorHandling.CheckDSL
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
            [ present; email; maxLength 254 ]
            ContactEmail
            value

module Sku =
    let value (Sku value) = value

    let create value : Result<Sku, RefinementError> =
        Refine.withChecks
            "Sku"
            [ present; lengthBetween 3 12; matches "^[A-Z0-9-]+$" ]
            Sku
            value

module Rating =
    let value (Rating value) = value

    let create value : Result<Rating, RefinementError> =
        Refine.withCheck "Rating" (Check.between 1 5) Rating value

module UnitPrice =
    let value (UnitPrice value) = value

    let create value : Result<UnitPrice, RefinementError> =
        Refine.withCheck "UnitPrice" (greaterThan 0m) UnitPrice value

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

This example shows total domain conversions built with Schema.convert, composed into a record schema, and lowered to executable checks.

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
open Axial.Schema.Syntax

/// <summary>An email address refined over Axial's text primitive, carrying the well-known email format.</summary>
type Email = private Email of string

module Email =
    let create (value: string) = Email value
    let value (Email value) = value

    let schema : Schema<Email> =
        Schema.text
        |> Schema.constrain Constraint.required
        |> Schema.convert create value
        |> Schema.constrain Constraint.email
        |> Schema.withFormat SchemaFormat.email

/// <summary>A bounded-text domain value whose length constraints live on the raw text schema.</summary>
type ContactName = private ContactName of string

module ContactName =
    let create (value: string) = ContactName value
    let value (ContactName value) = value

    let schema : Schema<ContactName> =
        Schema.text
        |> Schema.constrainAll [ Constraint.minLength 2; Constraint.maxLength 40 ]
        |> Schema.convert create value

/// <summary>A quantity that must always be positive (strictly greater than zero).</summary>
type Quantity = private Quantity of int

module Quantity =
    let create (value: int) = Quantity value
    let value (Quantity value) = value

    let schema : Schema<Quantity> =
        Schema.int
        |> Schema.constrain (Constraint.greaterThan 0)
        |> Schema.convert create value

/// <summary>A running total that must never go negative, but zero is allowed.</summary>
type Balance = private Balance of decimal

module Balance =
    let create (value: decimal) = Balance value
    let value (Balance value) = value

    let schema : Schema<Balance> =
        Schema.decimal
        |> Schema.constrain (Constraint.atLeast 0m)
        |> Schema.convert create value

type Contact =
    { Email: Email
      Name: ContactName
      Quantity: Quantity
      Balance: Balance }

let contactSchema =
    Schema.define<Contact>
    |> fieldWith Email.schema "email" _.Email
    |> fieldWith ContactName.schema "name" _.Name
    |> fieldWith Quantity.schema "quantity" _.Quantity
    |> fieldWith Balance.schema "balance" _.Balance
    |> construct (fun email name quantity balance ->
        { Email = email
          Name = name
          Quantity = quantity
          Balance = balance })

let run () =
    let contact =
        { Email = Email.create "ada@example.com"
          Name = ContactName.create "Ada"
          Quantity = Quantity.create 3
          Balance = Balance.create 0m }

    let emailCheck = Email.schema |> SchemaCheck.text
    let nameCheck = ContactName.schema |> SchemaCheck.text
    let quantityCheck = Quantity.schema |> SchemaCheck.ordered<int, _>
    let balanceCheck = Balance.schema |> SchemaCheck.ordered<decimal, _>

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
open System.Net
open System.Net.Http
open System.Threading.Tasks
open System.Text
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Axial.Schema
open Axial.Schema.Http
open Axial.Schema.Http.AspNetCore
open Axial.Codec
open Axial.Flow

// ---------------------------------------------------------------------------
// Domain model: parse, don't validate. Email can only be constructed by the
// schema (or its own tryParse), so a Signup in hand is always trusted.
// ---------------------------------------------------------------------------

type Email = private EmailValue of string

module Email =
    let value (EmailValue raw) = raw

    let schema: Schema<Email> =
        Schema.text
        |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 254; Constraint.email ]
        |> Schema.convert EmailValue value
        |> Schema.withFormat SchemaFormat.email

type Address = { Street: string; City: string }

type Signup =
    { Name: string
      Email: Email
      Age: int
      Address: Address
      Tags: string list }

module Signup =
    open Axial.Schema.Syntax

    let addressSchema =
        Schema.define<Address>
        |> fieldWith (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 120 ]) "street" _.Street
        |> fieldWith (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 80 ]) "city" _.City
        |> construct (fun street city -> { Street = street; City = city })

    let schema =
        Schema.define<Signup>
        |> fieldWith (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 80 ]) "name" _.Name
        |> fieldWith Email.schema "email" _.Email
        |> fieldWith (Schema.int |> Schema.constrain (Constraint.between 13 120)) "age" _.Age
        |> fieldWith (addressSchema |> Schema.constrain Constraint.required) "address" _.Address
        |> fieldWith (Schema.listWith Schema.text |> Schema.constrain (Constraint.maxCount 5)) "tags" _.Tags
        |> construct (fun name email age address tags ->
            { Name = name
              Email = email
              Age = age
              Address = address
              Tags = tags })

// ---------------------------------------------------------------------------
// Interpreters compiled once from the declaration above.
// ---------------------------------------------------------------------------

module Boundary =
    let codec = Json.compile Signup.schema

    // The request and response schemas are generated straight from the declaration,
    // so the published contract can never drift from what the parser accepts.
    let openApiDocument =
        OpenApi.document
            (OpenApi.info "Axial signup sample" "1.0.0")
            [ Endpoint.post "/signups"
              |> Endpoint.summary "Create a signup"
              |> Endpoint.accepts Signup.schema
              |> Endpoint.returnsJson 201 "The trusted signup that was parsed." Signup.schema
              |> Endpoint.returnsProblemDetails ]

// ---------------------------------------------------------------------------
// A small HTML form renderer over the schema's inspection metadata.
// ---------------------------------------------------------------------------

module FormPage =
    let private encode (text: string) = WebUtility.HtmlEncode text

    let private constraintAttributes (field: FieldDescription) =
        let metadata =
            (field.Constraints |> List.map _.Metadata)
            @ (let rec gather (value: SchemaDescription) =
                (value.Constraints |> List.map _.Metadata)
                @ (match value.Shape with
                   | SchemaShape.Refined underlying -> gather underlying
                   | _ -> [])

               gather field.Schema)

        let required =
            if metadata |> List.contains ConstraintMetadata.Required then " required" else ""

        let maxLength =
            metadata
            |> List.tryPick (function
                | ConstraintMetadata.MaxLength maximum -> Some $" maxlength=\"{maximum}\""
                | _ -> None)
            |> Option.defaultValue ""

        required + maxLength

    let private inputType (field: FieldDescription) =
        let rec shape (value: SchemaDescription) =
            match value.Shape with
            | SchemaShape.Refined underlying -> shape underlying
            | other -> other

        match field.Schema.Format, shape field.Schema with
        | Some format, _ when format = SchemaFormat.email -> "email"
        | _, SchemaShape.Primitive PrimitiveValueKind.Int -> "number"
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
                match field.Schema.Shape with
                | SchemaShape.Nested nested -> nested.Fields |> List.map (fieldRow field.Name)
                | SchemaShape.Many _ -> [ fieldRow "" field ]
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

let buildApp (args: string[]) =
    let builder = WebApplication.CreateBuilder(args)
    builder.Logging.ClearProviders() |> ignore
    let app = builder.Build()

    let acceptSignup (signup: Signup) : Flow<unit, string, Signup> =
        Flow.succeed signup

    let signupEndpoint =
        flow {
            let! signup = Request.json Signup.schema
            let! accepted = EndpointFlow.run acceptSignup signup
            return Response.json 201 Boundary.codec accepted
        }

    let endpoint =
        flowEndpoint
            (fun _ -> ())
            (fun error -> Results.BadRequest error)

    // ASP.NET owns the route. Axial turns the endpoint Flow into its native handler, parses the untrusted body,
    // supplies the application environment, and lowers the typed outcome back to an HTTP response.
    app.MapPost("/signups", endpoint signupEndpoint)
    |> ignore

    app.MapGet("/openapi.json", Func<IResult>(fun () -> SchemaResult.openApi Boundary.openApiDocument))
    |> ignore

    app.MapGet("/signup", Func<IResult>(fun () -> Results.Text(FormPage.render None, "text/html")))
    |> ignore

    app.MapPost(
        "/signup",
        Func<HttpRequest, Task<IResult>>(fun request ->
            task {
                let! parsed = SchemaRequest.form Signup.schema request
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
open Axial.Schema.Syntax

type Quantity = private Quantity of int

module Quantity =
    let create (value: int) = Quantity value
    let value (Quantity value) = value

    let schema : Schema<Quantity> =
        Schema.int
        |> Schema.constrain (Constraint.greaterThan 0)
        |> Schema.convert create value

type OrderLine =
    { Sku: string
      Quantity: Quantity }

let orderLineSchema =
    Schema.define<OrderLine>
    |> fieldWith Schema.text "sku" _.Sku
    |> fieldWith Quantity.schema "quantity" _.Quantity
    |> construct (fun sku quantity ->
        { Sku = sku
          Quantity = quantity })

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

// 3. Schema input result: adapt Schema.parse over raw boundary input.
let parseOrderLine : Policy<OrderEnv, OrderError, RawInput, OrderLine> =
    Policy.lift
        (fun raw -> (Schema.parse orderLineSchema raw).Result)
        (Diagnostics.flatten >> LineRejected)

// 4. Validation result: adapt intrinsic validation of an existing model.
let validateOrderLine : Policy<OrderEnv, OrderError, OrderLine, OrderLine> =
    Policy.lift
        (fun line -> Schema.check orderLineSchema line)
        (Diagnostics.flatten >> LineRejected)

// 5. Contextual rules: plain rule functions selected by the workflow environment.
let quantityCapRules (env: OrderEnv) : (OrderLine -> Result<unit, Diagnostics<OrderError>>) list =
    [ fun line ->
          if Quantity.value line.Quantity > env.MaxLineQuantity then
              ContextRules.failAt [ PathSegment.Name "quantity" ] (QuantityOverCap env.MaxLineQuantity)
          else
              Ok () ]

let underQuantityCap : Policy<OrderEnv, OrderError, OrderLine, OrderLine> =
    Policy.context
        (fun env line -> ContextRules.apply (quantityCapRules env) line)
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

