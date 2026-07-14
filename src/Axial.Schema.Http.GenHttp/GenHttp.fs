namespace Axial.Schema.Http.GenHttp

open System.Text.Json
open System.Threading.Tasks
open GenHTTP.Api.Protocol
open GenHTTP.Modules.IO
open Axial.Schema
open Axial.Schema.Http
open Axial.Codec

/// <summary>Parses GenHTTP requests into <see cref="T:Axial.Schema.ParsedInput`2" /> through a schema.</summary>
[<RequireQualifiedAccess>]
module SchemaRequest =
    /// <summary>Parses the JSON request body through the schema; a missing body parses as missing input.</summary>
    let json (schema: Schema<'model>) (request: IRequest) : ValueTask<ParsedInput<'model, SchemaError>> =
        match request.Content with
        | null -> ValueTask<_>(Schema.parse schema RawInput.Missing)
        | content ->
            let parse: Task<ParsedInput<'model, SchemaError>> =
                task {
                    use! document = JsonDocument.ParseAsync content
                    return Schema.parse schema (RawInput.ofJsonDocument document)
                }

            ValueTask<ParsedInput<'model, SchemaError>> parse

    /// <summary>Parses the query string through the schema.</summary>
    let query (schema: Schema<'model>) (request: IRequest) : ParsedInput<'model, SchemaError> =
        let pairs = request.Query |> Seq.map (fun pair -> pair.Key, pair.Value)
        Schema.parse schema (BoundaryInput.ofQuery pairs)

/// <summary>Builds GenHTTP responses for schema-driven endpoints.</summary>
[<RequireQualifiedAccess>]
module SchemaResponse =
    /// <summary>A 400 <c>application/problem+json</c> response rendering the failed parse's diagnostics.</summary>
    let problem (request: IRequest) (parsed: ParsedInput<'model, SchemaError>) : IResponse =
        match ProblemDetails.ofParsed parsed with
        | Some details ->
            request
                .Respond()
                .Status(ResponseStatus.BadRequest)
                .Content(ProblemDetails.toJson details)
                .Type(ProblemDetails.ContentType)
                .Build()
        | None -> request.Respond().Status(ResponseStatus.InternalServerError).Build()

    /// <summary>A JSON response rendering the trusted model through the compiled codec.</summary>
    let codec (codec: JsonCodec<'model>) (status: ResponseStatus) (request: IRequest) (value: 'model) : IResponse =
        request
            .Respond()
            .Status(status)
            .Content(Json.serialize codec value)
            .Type("application/json")
            .Build()

    /// <summary>Serves a pre-assembled OpenAPI document (see <see cref="M:Axial.Schema.Http.OpenApiModule.document" />).</summary>
    let openApi (request: IRequest) (document: string) : IResponse =
        request.Respond().Content(document).Type("application/json").Build()

    /// <summary>Runs the handler with the trusted model, or short-circuits to the problem-details response.</summary>
    let handleParsed
        (request: IRequest)
        (handler: 'model -> ValueTask<IResponse>)
        (parsed: ParsedInput<'model, SchemaError>)
        : ValueTask<IResponse> =
        match parsed.Result with
        | Ok model -> handler model
        | Error _ -> ValueTask<_>(problem request parsed)
