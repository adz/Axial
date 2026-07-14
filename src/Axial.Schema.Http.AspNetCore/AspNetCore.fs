namespace Axial.Schema.Http.AspNetCore

open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Axial.Schema
open Axial.Schema.Http
open Axial.Codec

/// <summary>Parses ASP.NET Core requests into <see cref="T:Axial.Schema.ParsedInput`2" /> through a schema.</summary>
[<RequireQualifiedAccess>]
module SchemaRequest =
    /// <summary>Parses the JSON request body through the schema.</summary>
    let json (schema: Schema<'model>) (request: HttpRequest) : Task<ParsedInput<'model, SchemaError>> =
        task {
            use! document = JsonDocument.ParseAsync request.Body
            return Schema.parse schema (RawInput.ofJsonDocument document)
        }

    /// <summary>Parses the posted form through the schema; dotted field names such as <c>address.street</c> nest.</summary>
    let form (schema: Schema<'model>) (request: HttpRequest) : Task<ParsedInput<'model, SchemaError>> =
        task {
            let! form = request.ReadFormAsync()

            let pairs =
                form
                |> Seq.collect (fun pair -> pair.Value |> Seq.map (fun value -> pair.Key, value))

            return Schema.parse schema (BoundaryInput.ofForm pairs)
        }

    /// <summary>Parses the query string through the schema.</summary>
    let query (schema: Schema<'model>) (request: HttpRequest) : ParsedInput<'model, SchemaError> =
        let pairs =
            request.Query
            |> Seq.collect (fun pair -> pair.Value |> Seq.map (fun value -> pair.Key, value))

        Schema.parse schema (BoundaryInput.ofQuery pairs)

/// <summary>Writes a trusted model straight to the response body through a compiled codec, without an intermediate string.</summary>
type private CodecResult<'model>(codec: JsonCodec<'model>, value: 'model, statusCode: int) =
    interface IResult with
        member _.ExecuteAsync(context: HttpContext) =
            context.Response.StatusCode <- statusCode
            context.Response.ContentType <- "application/json"

            match context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpBodyControlFeature>() with
            | null -> ()
            | feature -> feature.AllowSynchronousIO <- true

            Json.serializeToStream codec context.Response.Body value
            Task.CompletedTask

/// <summary>Builds <see cref="T:Microsoft.AspNetCore.Http.IResult" /> values for schema-driven endpoints.</summary>
[<RequireQualifiedAccess>]
module SchemaResult =
    /// <summary>A 400 <c>application/problem+json</c> response rendering the failed parse's diagnostics.</summary>
    let problem (parsed: ParsedInput<'model, SchemaError>) : IResult =
        match ProblemDetails.ofParsed parsed with
        | Some details -> Results.Text(ProblemDetails.toJson details, ProblemDetails.ContentType, statusCode = details.Status)
        | None -> Results.StatusCode 500

    /// <summary>A JSON response streaming the trusted model through the compiled codec.</summary>
    let codec (codec: JsonCodec<'model>) (statusCode: int) (value: 'model) : IResult =
        CodecResult(codec, value, statusCode) :> IResult

    /// <summary>Serves a pre-assembled OpenAPI document (see <see cref="M:Axial.Schema.Http.OpenApiModule.document" />).</summary>
    let openApi (document: string) : IResult =
        Results.Text(document, "application/json")

    /// <summary>Runs the handler with the trusted model, or short-circuits to the problem-details response.</summary>
    let handleParsed
        (handler: 'model -> Task<IResult>)
        (parsed: ParsedInput<'model, SchemaError>)
        : Task<IResult> =
        match parsed.Result with
        | Ok model -> handler model
        | Error _ -> Task.FromResult(problem parsed)
