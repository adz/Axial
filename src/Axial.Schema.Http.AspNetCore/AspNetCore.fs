namespace Axial.Schema.Http.AspNetCore

open System
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Axial.Schema
open Axial.Schema.Http
open Axial.Codec
open Axial.Flow

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

/// <summary>The request-scoped environment supplied to an ASP.NET Core endpoint Flow.</summary>
type HttpEndpointEnv<'app> =
    { /// <summary>The application's explicit services and request-derived domain context.</summary>
      App: 'app
      /// <summary>The native request, used by boundary operations in the adapter.</summary>
      Request: HttpRequest }

/// <summary>Distinguishes invalid request input from an expected application failure.</summary>
[<RequireQualifiedAccess>]
type EndpointError<'error> =
    /// <summary>The request could not be parsed into the declared trusted input.</summary>
    | InvalidRequest of ProblemDetails
    /// <summary>The application workflow failed with its typed error.</summary>
    | ApplicationError of 'error

/// <summary>Request decoders that contribute trusted values to an endpoint Flow.</summary>
[<RequireQualifiedAccess>]
module Request =
    let private fromParsed (parsed: ParsedInput<'model, SchemaError>) =
        match parsed.Result with
        | Ok model -> Flow.succeed model
        | Error diagnostics ->
            Flow.fail (EndpointError.InvalidRequest(ProblemDetails.ofDiagnostics diagnostics))

    let private parsed
        (parse: HttpRequest -> Task<ParsedInput<'model, SchemaError>>)
        : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.read _.Request
        |> Flow.bind (fun request ->
            parse request
            |> Flow.fromTask
            |> Flow.bind fromParsed)

    /// <summary>Reads and schema-parses a JSON request body.</summary>
    /// <example><code>let! signup = Request.json Signup.schema</code></example>
    let json (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.read _.Request
        |> Flow.bind (fun request ->
            task {
                try
                    let! parsed = SchemaRequest.json schema request
                    return Ok parsed
                with :? JsonException ->
                    return Error ProblemDetails.malformedJson
            }
            |> Flow.fromTask
            |> Flow.bind (function
                | Ok parsed -> fromParsed parsed
                | Error problem -> Flow.fail (EndpointError.InvalidRequest problem)))

    /// <summary>Reads and schema-parses a posted form.</summary>
    /// <example><code>let! signup = Request.form Signup.schema</code></example>
    let form (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        parsed (SchemaRequest.form schema)

    /// <summary>Schema-parses the query string.</summary>
    /// <example><code>let! search = Request.query Search.schema</code></example>
    let query (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.read _.Request
        |> Flow.bind (SchemaRequest.query schema >> fromParsed)

    /// <summary>Schema-parses one named ASP.NET route value.</summary>
    /// <example><code>let! userId = Request.route "id" UserId.schema</code></example>
    let route
        (name: string)
        (schema: Schema<'model>)
        : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.read _.Request
        |> Flow.bind (fun request ->
            let found, value = request.RouteValues.TryGetValue name

            let input =
                if not found || isNull value then RawInput.Missing
                else RawInput.Scalar(string value)

            Schema.parse schema input |> fromParsed)

    /// <summary>Projects untrusted input directly from the native request without schema parsing.</summary>
    /// <example><code>let! signature = Request.raw (fun request -&gt; string request.Headers["x-signature"])</code></example>
    let raw (projection: HttpRequest -> 'input) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'input> =
        Flow.read (fun environment -> projection environment.Request)

    /// <summary>Returns the native ASP.NET request for host-specific boundary handling.</summary>
    /// <example><code>let! request = Request.native</code></example>
    let native<'app, 'error> : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, HttpRequest> =
        Flow.read _.Request

/// <summary>Embeds an application Flow into an HTTP endpoint Flow.</summary>
[<RequireQualifiedAccess>]
module EndpointFlow =
    /// <summary>Supplies the application environment and marks typed application failures.</summary>
    /// <example><code>let! created = EndpointFlow.run createSignup signup</code></example>
    let run
        (operation: 'input -> Flow<'app, 'error, 'output>)
        (input: 'input)
        : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'output> =
        operation input
        |> Flow.localEnv _.App
        |> Flow.mapError EndpointError.ApplicationError

/// <summary>Successful responses returned by endpoint Flows.</summary>
[<RequireQualifiedAccess>]
module Response =
    /// <summary>Streams a trusted value as JSON through a compiled codec.</summary>
    /// <example><code>return Response.json 201 Signup.codec signup</code></example>
    let json (statusCode: int) (codec: JsonCodec<'model>) (value: 'model) : IResult =
        SchemaResult.codec codec statusCode value

    /// <summary>Returns an empty response with the supplied status code.</summary>
    /// <example><code>return Response.empty 204</code></example>
    let empty (statusCode: int) : IResult =
        Results.StatusCode statusCode

    /// <summary>Returns a plain-text response.</summary>
    /// <example><code>return Response.text 200 "ready"</code></example>
    let text (statusCode: int) (value: string) : IResult =
        Results.Text(value, statusCode = statusCode)

    /// <summary>Returns a host-native ASP.NET result unchanged.</summary>
    /// <example><code>return Response.native (Results.Redirect "/login")</code></example>
    let native (result: IResult) : IResult = result

[<AutoOpen>]
module FlowEndpoint =
    let private trySingleFailure (cause: Cause<'error>) : 'error option =
        let rec loop current =
            match current with
            | Cause.Fail error -> Some error
            | Cause.Traced(inner, _) -> loop inner
            | _ -> None

        loop cause

    /// <summary>Lowers an endpoint Flow to the native ASP.NET Core handler expected by minimal-API routing.</summary>
    /// <example>
    /// <code>
    /// let endpoint = flowEndpoint AppEnv.fromContext ApiError.toResponse
    /// app.MapPost("/signups", endpoint signupEndpoint)
    /// </code>
    /// </example>
    let flowEndpoint
        (getAppEnvironment: HttpContext -> 'app)
        (mapApplicationError: 'error -> IResult)
        (workflow: Flow<HttpEndpointEnv<'app>, EndpointError<'error>, IResult>)
        : System.Func<HttpContext, Task<IResult>> =
        System.Func<HttpContext, Task<IResult>>(fun context ->
            task {
                let environment =
                    { App = getAppEnvironment context
                      Request = context.Request }

                let! exit = workflow.ToTask(environment, cancellationToken = context.RequestAborted)

                match exit with
                | Exit.Success response -> return response
                | Exit.Failure cause ->
                    match trySingleFailure cause with
                    | Some (EndpointError.InvalidRequest problem) ->
                        return Results.Text(ProblemDetails.toJson problem, ProblemDetails.ContentType, statusCode = problem.Status)
                    | Some (EndpointError.ApplicationError error) ->
                        return mapApplicationError error
                    | None ->
                        let defects = Cause.defects cause

                        match defects with
                        | [ defect ] -> return raise defect
                        | _ :: _ -> return raise (AggregateException("Endpoint Flow died with multiple defects.", defects))
                        | [] when Cause.isInterrupted cause ->
                            return raise (OperationCanceledException("Endpoint Flow was interrupted.", context.RequestAborted))
                        | [] ->
                            let rendered = Cause.prettyPrint (fun error -> string error) cause
                            return raise (InvalidOperationException($"Endpoint Flow failed with an unsupported composite cause: {rendered}"))
            })
