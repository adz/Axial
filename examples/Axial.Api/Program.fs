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

open Axial
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
open Axial.Schema.Json
open Axial.Flow

// ---------------------------------------------------------------------------
// Domain model: parse, don't validate. Email can only be constructed by the
// schema (or its own tryParse), so a Signup in hand is always trusted.
// ---------------------------------------------------------------------------

type Email =
    private
    | EmailValue of string

    member this.Value = let (EmailValue value) = this in value

    static member Schema(_: Email) : Schema<Email> =
        Schema.text
        |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 254; Constraint.email ]
        |> Schema.convert EmailValue _.Value
        |> Schema.withFormat SchemaFormat.email

module Email =
    let value (email: Email) = email.Value

    let schema: Schema<Email> = SchemaDefaults.Resolve()

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
        schema<Address> {
            field "street" _.Street {
                constrain (minLength 1)
                constrain (maxLength 120)
            }
            field "city" _.City {
                constrain (minLength 1)
                constrain (maxLength 80)
            }
            construct (fun street city -> { Street = street; City = city })
        }

    let schema =
        schema<Signup> {
            field "name" _.Name {
                constrain (minLength 1)
                constrain (maxLength 80)
            }
            field "email" _.Email
            field "age" _.Age {
                constrain (between 13 120)
            }
            field "address" _.Address {
                withSchema (addressSchema |> Schema.constrain Constraint.required)
            }
            field "tags" _.Tags {
                withSchema (Schema.listWith Schema.text)
                constrain (maxCount 5)
            }
            construct (fun name email age address tags ->
                { Name = name
                  Email = email
                  Age = age
                  Address = address
                  Tags = tags })
        }

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

    /// Renders one flat form from the schema description, redisplaying structured data and attaching errors by path.
    let render (parsed: RetainedParseResult<Signup> option) =
        let input =
            parsed |> Option.map _.Input |> Option.defaultValue (Data.Object [])

        let errorsFor path =
            match parsed with
            | None -> []
            | Some parsed -> parsed.ErrorsFor(path: string) |> List.map SchemaError.render

        let fieldRow prefix (field: FieldDescription) =
            let path = if prefix = "" then field.Name else $"{prefix}.{field.Name}"
            let value = Data.redisplayPath path input

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
