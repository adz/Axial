namespace Axial.Schema.Http.AspNetCore

open Axial

open System
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Axial.Schema
open Axial.Schema.Http
open Axial.Codec
open Axial.Flow

/// <summary>Parses ASP.NET Core requests into <see cref="T:Axial.Schema.RetainedParseResult`2" /> through a schema.</summary>
[<RequireQualifiedAccess>]
module SchemaRequest =
    /// <summary>Parses the JSON request body through the schema.</summary>
    let json (schema: Schema<'model>) (request: HttpRequest) : Task<RetainedParseResult<'model, SchemaError>> =
        task {
            use! document = JsonDocument.ParseAsync request.Body
            return Schema.parseRetainingInput schema (Data.ofJsonDocument document)
        }

    /// <summary>Parses the posted form through the schema; dotted field names such as <c>address.street</c> nest.</summary>
    let form (schema: Schema<'model>) (request: HttpRequest) : Task<RetainedParseResult<'model, SchemaError>> =
        task {
            let! form = request.ReadFormAsync()

            let pairs =
                form
                |> Seq.collect (fun pair -> pair.Value |> Seq.map (fun value -> pair.Key, value))

            return Schema.parseRetainingInput schema (BoundaryInput.ofForm pairs)
        }

    /// <summary>Parses the query string through the schema.</summary>
    let query (schema: Schema<'model>) (request: HttpRequest) : RetainedParseResult<'model, SchemaError> =
        let pairs =
            request.Query
            |> Seq.collect (fun pair -> pair.Value |> Seq.map (fun value -> pair.Key, value))

        Schema.parseRetainingInput schema (BoundaryInput.ofQuery pairs)

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
    let problem (parsed: RetainedParseResult<'model, SchemaError>) : IResult =
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
        (parsed: RetainedParseResult<'model, SchemaError>)
        : Task<IResult> =
        match parsed.Result with
        | Ok model -> handler model
        | Error _ -> Task.FromResult(problem parsed)

/// <summary>The request-scoped environment supplied to an ASP.NET Core endpoint Flow.</summary>
/// <remarks>The host factory supplies <c>App</c>; adapter request operations read <c>Request</c>. Keep application workflows typed against <c>'app</c> and embed them with <c>EndpointFlow.run</c>.</remarks>
type HttpEndpointEnv<'app> =
    { /// <summary>The application's explicit services and request-derived domain context.</summary>
      App: 'app
      /// <summary>The native request, used by boundary operations in the adapter.</summary>
      Request: HttpRequest }

/// <summary>Distinguishes invalid request input from an expected application failure.</summary>
/// <remarks>Request operations create <c>InvalidRequest</c>; <c>EndpointFlow.run</c> wraps the application error channel as <c>ApplicationError</c>. <c>flowEndpoint</c> renders the two cases separately.</remarks>
[<RequireQualifiedAccess>]
type EndpointError<'error> =
    /// <summary>The request could not be parsed into the declared trusted input.</summary>
    | InvalidRequest of ProblemDetails
    /// <summary>The application workflow failed with its typed error.</summary>
    | ApplicationError of 'error

/// <summary>Request decoders that contribute trusted values to an endpoint Flow.</summary>
[<RequireQualifiedAccess>]
module Request =
    let private fromParsed (parsed: RetainedParseResult<'model, SchemaError>) =
        match parsed.Result with
        | Ok model -> Flow.succeed model
        | Error diagnostics ->
            Flow.fail (EndpointError.InvalidRequest(ProblemDetails.ofDiagnostics diagnostics))

    let private parsed
        (parse: HttpRequest -> Task<RetainedParseResult<'model, SchemaError>>)
        : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.read _.Request
        |> Flow.bind (fun request ->
            parse request
            |> Flow.fromTask
            |> Flow.bind fromParsed)

    /// <summary>Reads and schema-parses a JSON request body; malformed JSON and schema diagnostics become invalid-request failures.</summary>
    /// <param name="schema">The schema that establishes the trusted input type.</param>
    /// <returns>An endpoint Flow that succeeds with the trusted model.</returns>
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
    /// <param name="schema">The schema that interprets the form name/value input.</param>
    /// <returns>An endpoint Flow that succeeds with the trusted model.</returns>
    /// <example><code>let! signup = Request.form Signup.schema</code></example>
    let form (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        parsed (SchemaRequest.form schema)

    /// <summary>Schema-parses the query string.</summary>
    /// <param name="schema">The schema that interprets the complete query input.</param>
    /// <returns>An endpoint Flow that succeeds with the trusted model.</returns>
    /// <example><code>let! search = Request.query Search.schema</code></example>
    let query (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.read _.Request
        |> Flow.bind (SchemaRequest.query schema >> fromParsed)

    /// <summary>Schema-parses one named ASP.NET route value.</summary>
    /// <param name="name">The route-value name registered in the ASP.NET route pattern.</param>
    /// <param name="schema">The schema that parses the scalar route text.</param>
    /// <returns>An endpoint Flow that succeeds with the trusted model.</returns>
    /// <example><code>let! userId = Request.route "id" UserId.schema</code></example>
    let route
        (name: string)
        (schema: Schema<'model>)
        : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.read _.Request
        |> Flow.bind (fun request ->
            let found, value = request.RouteValues.TryGetValue name

            let input =
                if not found || isNull value then Data.Null
                else Data.Text(string value)

            Schema.parseRetainingInput schema input |> fromParsed)

    /// <summary>Projects untrusted input directly from the native request without schema parsing.</summary>
    /// <param name="projection">The direct projection from the native request.</param>
    /// <returns>An endpoint Flow containing the projected, still-untrusted value.</returns>
    /// <example><code>let! signature = Request.raw (fun request -&gt; string request.Headers["x-signature"])</code></example>
    let raw (projection: HttpRequest -> 'input) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'input> =
        Flow.read (fun environment -> projection environment.Request)

    /// <summary>Returns the native ASP.NET request for host-specific boundary handling.</summary>
    /// <returns>An endpoint Flow containing the current native request.</returns>
    /// <example><code>let! request = Request.native</code></example>
    let native<'app, 'error> : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, HttpRequest> =
        Flow.read _.Request

/// <summary>Embeds an application Flow into an HTTP endpoint Flow.</summary>
[<RequireQualifiedAccess>]
module EndpointFlow =
    /// <summary>Supplies <c>HttpEndpointEnv.App</c> to the application workflow and marks its typed failures as application errors.</summary>
    /// <param name="operation">The HTTP-independent application workflow factory.</param>
    /// <param name="input">The trusted input supplied to the application operation.</param>
    /// <returns>The application operation adapted to the endpoint environment and error channel.</returns>
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
    /// <param name="statusCode">The successful HTTP status code.</param>
    /// <param name="codec">The compiled codec for the trusted output type.</param>
    /// <param name="value">The trusted output value.</param>
    /// <returns>An ASP.NET result that streams the encoded JSON.</returns>
    /// <example><code>return Response.json 201 Signup.codec signup</code></example>
    let json (statusCode: int) (codec: JsonCodec<'model>) (value: 'model) : IResult =
        SchemaResult.codec codec statusCode value

    /// <summary>Returns an empty response with the supplied status code.</summary>
    /// <param name="statusCode">The successful HTTP status code.</param>
    /// <returns>An empty ASP.NET result.</returns>
    /// <example><code>return Response.empty 204</code></example>
    let empty (statusCode: int) : IResult =
        Results.StatusCode statusCode

    /// <summary>Returns a plain-text response.</summary>
    /// <param name="statusCode">The successful HTTP status code.</param>
    /// <param name="value">The response text.</param>
    /// <returns>An ASP.NET plain-text result.</returns>
    /// <example><code>return Response.text 200 "ready"</code></example>
    let text (statusCode: int) (value: string) : IResult =
        Results.Text(value, statusCode = statusCode)

    /// <summary>Returns a host-native ASP.NET result unchanged.</summary>
    /// <param name="result">The result constructed through ASP.NET APIs.</param>
    /// <returns>The supplied result.</returns>
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
    /// <remarks>
    /// Invalid requests become RFC 9457 responses and typed application failures use <c>mapApplicationError</c>.
    /// A single defect is rethrown unchanged, multiple defects become <c>AggregateException</c>, interruption becomes
    /// <c>OperationCanceledException</c> with <c>HttpContext.RequestAborted</c>, and compound typed-only causes are rejected
    /// rather than reduced to an arbitrary failure.
    /// </remarks>
    /// <param name="getAppEnvironment">Constructs or resolves the explicit application environment for the current request.</param>
    /// <param name="mapApplicationError">Maps one expected application failure to an ASP.NET result.</param>
    /// <param name="workflow">The complete endpoint Flow to execute.</param>
    /// <returns>A native delegate suitable for <c>MapGet</c>, <c>MapPost</c>, and the other ASP.NET routing methods.</returns>
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
