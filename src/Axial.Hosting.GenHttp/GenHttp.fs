namespace Axial.Hosting.GenHttp

open Reified

open System
open System.Text.Json
open System.Threading.Tasks
open GenHTTP.Api.Protocol
open GenHTTP.Modules.IO
open Reified.Schema.Http
open Axial

/// <summary>Parses GenHTTP requests into <see cref="T:Reified.RetainedParseResult`1" /> through a schema.</summary>
[<RequireQualifiedAccess>]
module SchemaRequest =
    /// <summary>Parses the JSON request body through the schema; a missing body parses as missing input.</summary>
    let json (schema: Schema<'model>) (request: IRequest) (cancellationToken: System.Threading.CancellationToken) : ValueTask<RetainedParseResult<'model>> =
        match request.Content with
        | null -> ValueTask<_>(Schema.parseRetainingInput schema Data.Null)
        | content ->
            let parse: Task<RetainedParseResult<'model>> =
                task {
                    use! document = JsonDocument.ParseAsync(content, cancellationToken = cancellationToken)
                    return Schema.parseRetainingInput schema (Data.ofJsonDocument document)
                }

            ValueTask<RetainedParseResult<'model>> parse

    /// <summary>Parses the query string through the schema.</summary>
    let query (schema: Schema<'model>) (request: IRequest) : RetainedParseResult<'model> =
        let pairs = request.Query |> Seq.map (fun pair -> pair.Key, pair.Value)
        Schema.parseRetainingInput schema (BoundaryInput.ofQuery pairs)

    /// <summary>Returns <c>true</c> when the request body is <c>application/x-www-form-urlencoded</c>.</summary>
    let isForm (request: IRequest) : bool =
        not (isNull request.Content)
        && not (isNull (box request.ContentType))
        && String.Equals(request.ContentType.RawType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)

    /// <summary>Parses an <c>application/x-www-form-urlencoded</c> body through the schema; any other body parses as empty input.</summary>
    let form (schema: Schema<'model>) (request: IRequest) (cancellationToken: System.Threading.CancellationToken) : ValueTask<RetainedParseResult<'model>> =
        if not (isForm request) then
            ValueTask<_>(Schema.parseRetainingInput schema (BoundaryInput.ofForm []))
        else
            let decode (text: string) = Uri.UnescapeDataString(text.Replace('+', ' '))

            let parse: Task<RetainedParseResult<'model>> =
                task {
                    use reader = new System.IO.StreamReader(request.Content)
                    let! body = reader.ReadToEndAsync(cancellationToken)

                    let pairs =
                        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
                        |> Array.map (fun pair ->
                            match pair.IndexOf '=' with
                            | -1 -> decode pair, ""
                            | at -> decode pair[.. at - 1], decode pair[at + 1 ..])

                    return Schema.parseRetainingInput schema (BoundaryInput.ofForm pairs)
                }

            ValueTask<RetainedParseResult<'model>> parse

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

    /// <summary>Serves a pre-assembled OpenAPI document (see <see cref="M:Reified.Schema.Http.OpenApiModule.document" />).</summary>
    let openApi (request: IRequest) (document: string) : IResponse =
        request.Respond().Content(document).Type("application/json").Build()

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
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>let! signup = Request.json Signup.schema</code></example>
    let json (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.envWith _.Request
        |> Flow.bind (fun request ->
            Flow.fromTask (fun cancellationToken ->
                task {
                    cancellationToken.ThrowIfCancellationRequested()
                    try
                        let! parsed = (SchemaRequest.json schema request cancellationToken).AsTask()
                        return Ok parsed
                    with :? JsonException ->
                        return Error ProblemDetails.malformedJson
                })
            |> Flow.bind (function
                | Ok parsed -> fromParsed parsed
                | Error problem -> Flow.fail (EndpointError.InvalidRequest problem)))

    /// <summary>Schema-parses the query string.</summary>
    /// <param name="schema">The schema that interprets the complete query input.</param>
    /// <returns>An endpoint Flow that succeeds with the trusted model.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>let! search = Request.query Search.schema</code></example>
    let query (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.envWith _.Request
        |> Flow.bind (SchemaRequest.query schema >> fromParsed)

    let private notAForm : ProblemDetails =
        { ProblemDetails.malformedJson with
            Errors = [ { Pointer = ""; Message = "The request body is not a form." } ] }

    /// <summary>Reads and schema-parses a posted <c>application/x-www-form-urlencoded</c> form.</summary>
    /// <remarks>Any other body is an invalid request.</remarks>
    /// <param name="schema">The schema that interprets the form name/value input.</param>
    /// <returns>An endpoint Flow that succeeds with the trusted model.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>let! signup = Request.form Signup.schema</code></example>
    let form (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.envWith _.Request
        |> Flow.bind (fun request ->
            if SchemaRequest.isForm request then
                Flow.fromTask (fun token -> (SchemaRequest.form schema request token).AsTask())
                |> Flow.bind fromParsed
            else
                Flow.fail (EndpointError.InvalidRequest notAForm))

    /// <summary>Schema-parses a posted form and returns the outcome with the input it came from, for a page that shows the form again with its errors.</summary>
    /// <remarks>
    /// Unlike <c>form</c>, a parse failure does not end the endpoint. A body that is not a form parses as empty input,
    /// so every required field reports that it must be present.
    /// </remarks>
    /// <param name="schema">The schema that interprets the form name/value input.</param>
    /// <returns>An endpoint Flow that succeeds with the parse result: the trusted model, or the errors and the values to show again.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema and views are supplied by the application.">
    /// <code>
    /// match! Request.parseForm Signup.schema with
    /// | { Result = Ok signup } -> ...
    /// | invalid -> return Response.html ResponseStatus.UnprocessableEntity (SignupView.form invalid)
    /// </code>
    /// </example>
    let parseForm (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, RetainedParseResult<'model>> =
        Flow.envWith _.Request
        |> Flow.bind (fun request -> Flow.fromTask (fun token -> (SchemaRequest.form schema request token).AsTask()))

    /// <summary>Schema-parses the query string and returns the outcome with the input it came from, for a page that shows its filters again with their errors.</summary>
    /// <param name="schema">The schema that interprets the complete query input.</param>
    /// <returns>An endpoint Flow that succeeds with the parse result.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema is supplied by the application."><code>let! search = Request.parseQuery Search.schema</code></example>
    let parseQuery (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, RetainedParseResult<'model>> =
        Flow.envWith (fun environment -> SchemaRequest.query schema environment.Request)

    /// <summary>Projects untrusted input directly from the native request without schema parsing.</summary>
    /// <param name="projection">The direct projection from the native request.</param>
    /// <returns>An endpoint Flow containing the projected, still-untrusted value.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>let! signature = Request.raw (fun request -&gt; string request.Headers["x-signature"])</code></example>
    let raw (projection: IRequest -> 'input) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'input> =
        Flow.envWith (fun environment -> projection environment.Request)

    /// <summary>Returns the native GenHTTP request for host-specific boundary handling.</summary>
    /// <returns>An endpoint Flow containing the current native request.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>let! request = Request.native</code></example>
    let native<'app, 'error> : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, IRequest> =
        Flow.envWith _.Request

/// <summary>Embeds an application Flow into a GenHTTP endpoint Flow.</summary>
[<RequireQualifiedAccess>]
module EndpointFlow =
    /// <summary>Supplies <c>HttpEndpointEnv.App</c> to the application workflow and marks its typed failures as application errors.</summary>
    /// <param name="operation">The HTTP-independent application workflow factory.</param>
    /// <param name="input">The trusted input supplied to the application operation.</param>
    /// <returns>The application operation adapted to the endpoint environment and error channel.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>let! created = EndpointFlow.run createSignup signup</code></example>
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
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>return Response.json ResponseStatus.Created Signup.codec signup</code></example>
    let json (status: ResponseStatus) (codec: JsonCodec<'model>) (value: 'model) : HttpResponse =
        HttpResponse(fun request -> SchemaResponse.codec codec status request value)

    /// <summary>Returns an empty response with the supplied status.</summary>
    /// <param name="status">The successful HTTP status.</param>
    /// <returns>A request-relative empty response plan.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>return Response.empty ResponseStatus.NoContent</code></example>
    let empty (status: ResponseStatus) : HttpResponse =
        HttpResponse(fun request -> request.Respond().Status(status).Build())

    /// <summary>Returns a plain-text response.</summary>
    /// <param name="status">The successful HTTP status.</param>
    /// <param name="value">The response text.</param>
    /// <returns>A request-relative plain-text response plan.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>return Response.text ResponseStatus.Ok "ready"</code></example>
    let text (status: ResponseStatus) (value: string) : HttpResponse =
        HttpResponse(fun request -> request.Respond().Status(status).Content(value).Type("text/plain").Build())

    /// <summary>Returns an HTML response, such as a page or the fragment an htmx request swaps in.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="html">The rendered HTML.</param>
    /// <returns>A request-relative response plan with content type <c>text/html; charset=utf-8</c>.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the view is supplied by the application."><code>return Response.html ResponseStatus.Ok (renderHtml page)</code></example>
    let html (status: ResponseStatus) (html: string) : HttpResponse =
        HttpResponse(fun request ->
            request.Respond().Status(status).Content(html).Type(FlexibleContentType.Parse "text/html; charset=utf-8").Build())

    /// <summary>Returns a redirect to another address.</summary>
    /// <param name="status">The redirect status: <c>SeeOther</c> after a form post, <c>Found</c> or <c>TemporaryRedirect</c> for a temporary move, <c>MovedPermanently</c> or <c>PermanentRedirect</c> for a permanent one.</param>
    /// <param name="location">The address sent in the <c>Location</c> header.</param>
    /// <returns>A request-relative empty response plan with the status and <c>Location</c> header.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context."><code>return Response.redirect ResponseStatus.SeeOther "/signups"</code></example>
    let redirect (status: ResponseStatus) (location: string) : HttpResponse =
        HttpResponse(fun request -> request.Respond().Status(status).Header("Location", location).Build())

    /// <summary>Adds a header to a response, replacing a value already set under that name.</summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <param name="response">The response plan to add the header to.</param>
    /// <returns>The response plan, sent with the header.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the view is supplied by the application.">
    /// <code>
    /// return
    ///     Response.html ResponseStatus.Ok fragment
    ///     |> Response.withHeader "HX-Push-Url" address
    ///     |> Response.withHeader "HX-Trigger" "saved"
    /// </code>
    /// </example>
    let withHeader (name: string) (value: string) (HttpResponse respond) : HttpResponse =
        HttpResponse(fun request ->
            let response = respond request

            // GenHTTP writes Content-Type from the response's ContentType, not from its header collection.
            if String.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase) then
                response.ContentType <- FlexibleContentType.Parse value
            else
                response.Headers[name] <- value

            response)

    /// <summary>Builds a host-native response plan from the current GenHTTP request.</summary>
    /// <param name="respond">The host-specific response function.</param>
    /// <returns>A request-relative response plan.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>return Response.native (fun request -&gt; request.Respond().Build())</code></example>
    let native (respond: IRequest -> IResponse) : HttpResponse =
        HttpResponse respond

/// <summary>Optional request log sink. None leaves the runtime default; LogSink.none disables logging.</summary>
type EndpointOptions =
    { LogSink: ILogSink option }
    static member Default = { LogSink = None }

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
    /// <param name="options">Optional log sink; None leaves the runtime default unchanged.</param>
    /// <param name="getAppEnvironment">Constructs or resolves the explicit application environment for the current request.</param>
    /// <param name="mapApplicationError">Maps one expected application failure to a GenHTTP response plan.</param>
    /// <param name="workflow">The complete endpoint Flow to execute.</param>
    /// <returns>A native delegate suitable for GenHTTP routing methods.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application.">
    /// <code>
    /// let endpoint = flowEndpoint getAppEnvironment ApiError.toResponse
    /// Inline.Create().Post("/signups", endpoint signupEndpoint)
    /// </code>
    /// </example>
    let flowEndpointWithOptions
        (options: EndpointOptions)
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

                    let hosted =
                        match options.LogSink with
                        | Some sink -> workflow |> Flow.withLogSink sink
                        | None -> workflow
                    let! exit = hosted.StartAsValueTask(environment).AsTask()

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
                                let rendered = Cause.prettyPrint (function
                                    | EndpointError.InvalidRequest _ -> "invalid request"
                                    | EndpointError.ApplicationError _ -> "application error") cause
                                return raise (InvalidOperationException($"Endpoint Flow failed with an unsupported composite cause: {rendered}"))
                }))

    /// <summary>Runs a request with the runtime default log sink unless the flow installs its own.</summary>
    let flowEndpoint getAppEnvironment mapApplicationError workflow =
        flowEndpointWithOptions EndpointOptions.Default getAppEnvironment mapApplicationError workflow
