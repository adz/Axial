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
