namespace Axial.Schema.Http.GenHttp

open Axial

open System
open System.Text.Json
open System.Threading.Tasks
open GenHTTP.Api.Protocol
open GenHTTP.Modules.IO
open Axial.Schema
open Axial.Schema.Http
open Axial.Schema.Json
open Axial.Flow

/// <summary>Parses GenHTTP requests into <see cref="T:Axial.Schema.RetainedParseResult`2" /> through a schema.</summary>
[<RequireQualifiedAccess>]
module SchemaRequest =
    /// <summary>Parses the JSON request body through the schema; a missing body parses as missing input.</summary>
    let json (schema: Schema<'model>) (request: IRequest) : ValueTask<RetainedParseResult<'model>> =
        match request.Content with
        | null -> ValueTask<_>(Schema.parseRetainingInput schema Data.Null)
        | content ->
            let parse: Task<RetainedParseResult<'model>> =
                task {
                    use! document = JsonDocument.ParseAsync content
                    return Schema.parseRetainingInput schema (Data.ofJsonDocument document)
                }

            ValueTask<RetainedParseResult<'model>> parse

    /// <summary>Parses the query string through the schema.</summary>
    let query (schema: Schema<'model>) (request: IRequest) : RetainedParseResult<'model> =
        let pairs = request.Query |> Seq.map (fun pair -> pair.Key, pair.Value)
        Schema.parseRetainingInput schema (BoundaryInput.ofQuery pairs)

/// <summary>Builds GenHTTP responses for schema-driven endpoints.</summary>
[<RequireQualifiedAccess>]
module SchemaResponse =
    /// <summary>A 400 <c>application/problem+json</c> response rendering the failed parse's diagnostics.</summary>
    let problem (request: IRequest) (parsed: RetainedParseResult<'model>) : IResponse =
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
        (parsed: RetainedParseResult<'model>)
        : ValueTask<IResponse> =
        match parsed.Result with
        | Ok model -> handler model
        | Error _ -> ValueTask<_>(problem request parsed)

/// <summary>The request-scoped environment supplied to a GenHTTP endpoint Flow.</summary>
/// <remarks>The host factory supplies <c>App</c>; adapter request operations read <c>Request</c>. Keep application workflows typed against <c>'app</c> and embed them with <c>EndpointFlow.run</c>.</remarks>
type HttpEndpointEnv<'app> =
    { /// <summary>The application's explicit services and request-derived domain context.</summary>
      App: 'app
      /// <summary>The native request, used by boundary operations in the adapter.</summary>
      Request: IRequest }

/// <summary>Distinguishes invalid request input from an expected application failure.</summary>
/// <remarks>Request operations create <c>InvalidRequest</c>; <c>EndpointFlow.run</c> wraps the application error channel as <c>ApplicationError</c>. <c>flowEndpoint</c> renders the two cases separately.</remarks>
[<RequireQualifiedAccess>]
type EndpointError<'error> =
    /// <summary>The request could not be parsed into the declared trusted input.</summary>
    | InvalidRequest of ProblemDetails
    /// <summary>The application workflow failed with its typed error.</summary>
    | ApplicationError of 'error

/// <summary>A response plan that GenHTTP executes against the current native request.</summary>
/// <remarks>The plan stays opaque so successful and typed-error responses are constructed from the same request that entered <c>flowEndpoint</c>.</remarks>
type HttpResponse = private HttpResponse of (IRequest -> IResponse)

/// <summary>Request decoders that contribute trusted values to an endpoint Flow.</summary>
[<RequireQualifiedAccess>]
module Request =
    let private fromParsed (parsed: RetainedParseResult<'model>) =
        match parsed.Result with
        | Ok model -> Flow.succeed model
        | Error diagnostics ->
            Flow.fail (EndpointError.InvalidRequest(ProblemDetails.ofErrors diagnostics))

    /// <summary>Reads and schema-parses a JSON request body; malformed JSON and schema diagnostics become invalid-request failures.</summary>
    /// <param name="schema">The schema that establishes the trusted input type.</param>
    /// <returns>An endpoint Flow that succeeds with the trusted model.</returns>
    /// <example><code>let! signup = Request.json Signup.schema</code></example>
    let json (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.read _.Request
        |> Flow.bind (fun request ->
            task {
                try
                    let! parsed = (SchemaRequest.json schema request).AsTask()
                    return Ok parsed
                with :? JsonException ->
                    return Error ProblemDetails.malformedJson
            }
            |> Flow.fromTask
            |> Flow.bind (function
                | Ok parsed -> fromParsed parsed
                | Error problem -> Flow.fail (EndpointError.InvalidRequest problem)))

    /// <summary>Schema-parses the query string.</summary>
    /// <param name="schema">The schema that interprets the complete query input.</param>
    /// <returns>An endpoint Flow that succeeds with the trusted model.</returns>
    /// <example><code>let! search = Request.query Search.schema</code></example>
    let query (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.read _.Request
        |> Flow.bind (SchemaRequest.query schema >> fromParsed)

    /// <summary>Projects untrusted input directly from the native request without schema parsing.</summary>
    /// <param name="projection">The direct projection from the native request.</param>
    /// <returns>An endpoint Flow containing the projected, still-untrusted value.</returns>
    /// <example><code>let! signature = Request.raw (fun request -&gt; string request.Headers["x-signature"])</code></example>
    let raw (projection: IRequest -> 'input) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'input> =
        Flow.read (fun environment -> projection environment.Request)

    /// <summary>Returns the native GenHTTP request for host-specific boundary handling.</summary>
    /// <returns>An endpoint Flow containing the current native request.</returns>
    /// <example><code>let! request = Request.native</code></example>
    let native<'app, 'error> : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, IRequest> =
        Flow.read _.Request

/// <summary>Embeds an application Flow into a GenHTTP endpoint Flow.</summary>
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

/// <summary>Successful responses returned by GenHTTP endpoint Flows.</summary>
[<RequireQualifiedAccess>]
module Response =
    /// <summary>Serializes a trusted value as JSON through a compiled codec.</summary>
    /// <param name="status">The successful HTTP status.</param>
    /// <param name="codec">The compiled codec for the trusted output type.</param>
    /// <param name="value">The trusted output value.</param>
    /// <returns>A request-relative GenHTTP response plan.</returns>
    /// <example><code>return Response.json ResponseStatus.Created Signup.codec signup</code></example>
    let json (status: ResponseStatus) (codec: JsonCodec<'model>) (value: 'model) : HttpResponse =
        HttpResponse(fun request -> SchemaResponse.codec codec status request value)

    /// <summary>Returns an empty response with the supplied status.</summary>
    /// <param name="status">The successful HTTP status.</param>
    /// <returns>A request-relative empty response plan.</returns>
    /// <example><code>return Response.empty ResponseStatus.NoContent</code></example>
    let empty (status: ResponseStatus) : HttpResponse =
        HttpResponse(fun request -> request.Respond().Status(status).Build())

    /// <summary>Returns a plain-text response.</summary>
    /// <param name="status">The successful HTTP status.</param>
    /// <param name="value">The response text.</param>
    /// <returns>A request-relative plain-text response plan.</returns>
    /// <example><code>return Response.text ResponseStatus.Ok "ready"</code></example>
    let text (status: ResponseStatus) (value: string) : HttpResponse =
        HttpResponse(fun request -> request.Respond().Status(status).Content(value).Type("text/plain").Build())

    /// <summary>Builds a host-native response plan from the current GenHTTP request.</summary>
    /// <param name="respond">The host-specific response function.</param>
    /// <returns>A request-relative response plan.</returns>
    /// <example><code>return Response.native (fun request -&gt; request.Respond().Build())</code></example>
    let native (respond: IRequest -> IResponse) : HttpResponse =
        HttpResponse respond

[<AutoOpen>]
module FlowEndpoint =
    let private trySingleFailure (cause: Cause<'error>) : 'error option =
        let rec loop current =
            match current with
            | Cause.Fail error -> Some error
            | Cause.Traced(inner, _) -> loop inner
            | _ -> None

        loop cause

    let private problemResponse (request: IRequest) (problem: ProblemDetails) =
        request
            .Respond()
            .Status(ResponseStatus.BadRequest)
            .Content(ProblemDetails.toJson problem)
            .Type(ProblemDetails.ContentType)
            .Build()

    /// <summary>Lowers an endpoint Flow to the native handler expected by GenHTTP routing.</summary>
    /// <remarks>
    /// Invalid requests become RFC 9457 responses and typed application failures use <c>mapApplicationError</c>.
    /// A single defect is rethrown unchanged, multiple defects become <c>AggregateException</c>, interruption becomes
    /// <c>OperationCanceledException</c>, and compound typed-only causes are rejected rather than reduced to an arbitrary
    /// failure. GenHTTP does not supply a request cancellation token through this adapter.
    /// </remarks>
    /// <param name="getAppEnvironment">Constructs or resolves the explicit application environment for the current request.</param>
    /// <param name="mapApplicationError">Maps one expected application failure to a GenHTTP response plan.</param>
    /// <param name="workflow">The complete endpoint Flow to execute.</param>
    /// <returns>A native delegate suitable for GenHTTP routing methods.</returns>
    /// <example>
    /// <code>
    /// let endpoint = flowEndpoint getAppEnvironment ApiError.toResponse
    /// Inline.Create().Post("/signups", endpoint signupEndpoint)
    /// </code>
    /// </example>
    let flowEndpoint
        (getAppEnvironment: IRequest -> 'app)
        (mapApplicationError: 'error -> HttpResponse)
        (workflow: Flow<HttpEndpointEnv<'app>, EndpointError<'error>, HttpResponse>)
        : Func<IRequest, ValueTask<IResponse>> =
        Func<IRequest, ValueTask<IResponse>>(fun request ->
            ValueTask<IResponse>(
                task {
                    let environment =
                        { App = getAppEnvironment request
                          Request = request }

                    let! exit = workflow.ToValueTask(environment).AsTask()

                    match exit with
                    | Exit.Success (HttpResponse respond) -> return respond request
                    | Exit.Failure cause ->
                        match trySingleFailure cause with
                        | Some (EndpointError.InvalidRequest problem) ->
                            return problemResponse request problem
                        | Some (EndpointError.ApplicationError error) ->
                            let (HttpResponse respond) = mapApplicationError error
                            return respond request
                        | None ->
                            let defects = Cause.defects cause

                            match defects with
                            | [ defect ] -> return raise defect
                            | _ :: _ -> return raise (AggregateException("Endpoint Flow died with multiple defects.", defects))
                            | [] when Cause.isInterrupted cause ->
                                return raise (OperationCanceledException("Endpoint Flow was interrupted."))
                            | [] ->
                                let rendered = Cause.prettyPrint (fun error -> string error) cause
                                return raise (InvalidOperationException($"Endpoint Flow failed with an unsupported composite cause: {rendered}"))
                }))
