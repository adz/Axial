namespace Axial.Hosting.AspNetCore

open Reified

open System
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Axial.Hosting
open Reified.Schema.Http
open Axial

/// <summary>Parses ASP.NET Core requests into <see cref="T:Reified.RetainedParseResult`1" /> through a schema.</summary>
[<RequireQualifiedAccess>]
module SchemaRequest =
    /// <summary>Parses the JSON request body through the schema.</summary>
    let json (schema: Schema<'model>) (request: HttpRequest) (cancellationToken: System.Threading.CancellationToken) : Task<RetainedParseResult<'model>> =
        task {
            use! document = JsonDocument.ParseAsync(request.Body, cancellationToken = cancellationToken)
            return Schema.parseRetainingInput schema (Data.ofJsonDocument document)
        }

    /// <summary>Parses the posted form through the schema; dotted field names such as <c>address.street</c> nest.</summary>
    let form (schema: Schema<'model>) (request: HttpRequest) (cancellationToken: System.Threading.CancellationToken) : Task<RetainedParseResult<'model>> =
        task {
            let! form = request.ReadFormAsync(cancellationToken)

            let pairs =
                form
                |> Seq.collect (fun pair -> pair.Value |> Seq.map (fun value -> pair.Key, value))

            return Schema.parseRetainingInput schema (BoundaryInput.ofForm pairs)
        }

    /// <summary>Parses the query string through the schema.</summary>
    let query (schema: Schema<'model>) (request: HttpRequest) : RetainedParseResult<'model> =
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
    let problem (parsed: RetainedParseResult<'model>) : IResult =
        match ProblemDetails.ofParsed parsed with
        | Some details -> Results.Text(ProblemDetails.toJson details, ProblemDetails.ContentType, statusCode = details.Status)
        | None -> Results.StatusCode 500

    /// <summary>A JSON response streaming the trusted model through the compiled codec.</summary>
    let codec (codec: JsonCodec<'model>) (statusCode: int) (value: 'model) : IResult =
        CodecResult(codec, value, statusCode) :> IResult

    /// <summary>Serves a pre-assembled OpenAPI document (see <see cref="M:Reified.Schema.Http.OpenApiModule.document" />).</summary>
    let openApi (document: string) : IResult =
        Results.Text(document, "application/json")

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
    let private fromParsed (parsed: RetainedParseResult<'model>) =
        match parsed.Result with
        | Ok model -> Flow.succeed model
        | Error diagnostics ->
            Flow.fail (EndpointError.InvalidRequest(ProblemDetails.ofErrors diagnostics))

    let private bodyTooLarge : ProblemDetails =
        { ProblemDetails.malformedJson with
            Title = "The request body is too large."
            Status = 413
            Errors = [ { Pointer = ""; Message = "The request body is larger than this endpoint accepts." } ] }

    /// <summary>Reads and schema-parses a JSON request body; malformed JSON and schema diagnostics become invalid-request failures.</summary>
    /// <remarks>A body larger than the server's limit (see <c>EndpointOptions.MaxRequestBodySize</c>) is a 413 problem response.</remarks>
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
                        let! parsed = SchemaRequest.json schema request cancellationToken
                        return Ok parsed
                    with
                    | :? JsonException -> return Error ProblemDetails.malformedJson
                    | :? BadHttpRequestException as error when error.StatusCode = 413 -> return Error bodyTooLarge
                })
            |> Flow.bind (function
                | Ok parsed -> fromParsed parsed
                | Error problem -> Flow.fail (EndpointError.InvalidRequest problem)))

    let private notAForm : ProblemDetails =
        { ProblemDetails.malformedJson with
            Errors = [ { Pointer = ""; Message = "The request body is not a form." } ] }

    let private formTooLarge : ProblemDetails =
        { ProblemDetails.malformedJson with
            Title = "The request body is too large."
            Status = 413
            Errors = [ { Pointer = ""; Message = "The form is larger than the form reader's limits allow." } ] }

    // ReadFormAsync throws InvalidDataException past FormOptions limits (value length, value count, multipart body).
    let private readForm (schema: Schema<'model>) (request: HttpRequest) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, RetainedParseResult<'model>> =
        Flow.fromTask (fun cancellationToken ->
            task {
                try
                    let! parsed = SchemaRequest.form schema request cancellationToken
                    return Ok parsed
                with
                | :? System.IO.InvalidDataException -> return Error formTooLarge
                | :? BadHttpRequestException as error when error.StatusCode = 413 -> return Error bodyTooLarge
            })
        |> Flow.bind (function
            | Ok parsed -> Flow.succeed parsed
            | Error problem -> Flow.fail (EndpointError.InvalidRequest problem))

    /// <summary>Reads and schema-parses a posted form.</summary>
    /// <remarks>A body that is not <c>application/x-www-form-urlencoded</c> or <c>multipart/form-data</c> is an invalid request. A form past ASP.NET Core's <c>FormOptions</c> limits is a 413 problem response.</remarks>
    /// <param name="schema">The schema that interprets the form name/value input.</param>
    /// <returns>An endpoint Flow that succeeds with the trusted model.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>let! signup = Request.form Signup.schema</code></example>
    let form (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.envWith _.Request
        |> Flow.bind (fun request ->
            if request.HasFormContentType then
                readForm schema request |> Flow.bind fromParsed
            else
                Flow.fail (EndpointError.InvalidRequest notAForm))

    /// <summary>Schema-parses the query string.</summary>
    /// <param name="schema">The schema that interprets the complete query input.</param>
    /// <returns>An endpoint Flow that succeeds with the trusted model.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>let! search = Request.query Search.schema</code></example>
    let query (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.envWith _.Request
        |> Flow.bind (SchemaRequest.query schema >> fromParsed)

    /// <summary>Schema-parses a posted form and returns the outcome with the input it came from, for a page that shows the form again with its errors.</summary>
    /// <remarks>
    /// Unlike <c>form</c>, a parse failure does not end the endpoint. A body that is not a form parses as empty input,
    /// so every required field reports that it must be present. A form past ASP.NET Core's <c>FormOptions</c> limits
    /// still ends the endpoint with a 413 problem response.
    /// </remarks>
    /// <param name="schema">The schema that interprets the form name/value input.</param>
    /// <returns>An endpoint Flow that succeeds with the parse result: the trusted model, or the errors and the values to show again.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema and views are supplied by the application.">
    /// <code>
    /// match! Request.parseForm Signup.schema with
    /// | { Result = Ok signup } -> ...
    /// | invalid -> return Response.html 422 (SignupView.form invalid)
    /// </code>
    /// </example>
    let parseForm (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, RetainedParseResult<'model>> =
        Flow.envWith _.Request
        |> Flow.bind (fun request ->
            if request.HasFormContentType then
                readForm schema request
            else
                Flow.succeed (Schema.parseRetainingInput schema (BoundaryInput.ofForm [])))

    /// <summary>Schema-parses the query string and returns the outcome with the input it came from, for a page that shows its filters again with their errors.</summary>
    /// <param name="schema">The schema that interprets the complete query input.</param>
    /// <returns>An endpoint Flow that succeeds with the parse result.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema is supplied by the application."><code>let! search = Request.parseQuery Search.schema</code></example>
    let parseQuery (schema: Schema<'model>) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, RetainedParseResult<'model>> =
        Flow.envWith (fun environment -> SchemaRequest.query schema environment.Request)

    /// <summary>Schema-parses one named ASP.NET route value.</summary>
    /// <param name="name">The route-value name registered in the ASP.NET route pattern.</param>
    /// <param name="schema">The schema that parses the scalar route text.</param>
    /// <returns>An endpoint Flow that succeeds with the trusted model.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>let! userId = Request.route "id" UserId.schema</code></example>
    let route
        (name: string)
        (schema: Schema<'model>)
        : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'model> =
        Flow.envWith _.Request
        |> Flow.bind (fun request ->
            let found, value = request.RouteValues.TryGetValue name

            let input =
                if not found || isNull value then Data.Null
                else Data.Text(string value)

            Schema.parseRetainingInput schema input |> fromParsed)

    /// <summary>Projects untrusted input directly from the native request without schema parsing.</summary>
    /// <param name="projection">The direct projection from the native request.</param>
    /// <returns>An endpoint Flow containing the projected, still-untrusted value.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>let! signature = Request.raw (fun request -&gt; string request.Headers["x-signature"])</code></example>
    let raw (projection: HttpRequest -> 'input) : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, 'input> =
        Flow.envWith (fun environment -> projection environment.Request)

    /// <summary>Returns the native ASP.NET request for host-specific boundary handling.</summary>
    /// <returns>An endpoint Flow containing the current native request.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>let! request = Request.native</code></example>
    let native<'app, 'error> : Flow<HttpEndpointEnv<'app>, EndpointError<'error>, HttpRequest> =
        Flow.envWith _.Request

/// <summary>Embeds an application Flow into an HTTP endpoint Flow.</summary>
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

/// <summary>Successful responses returned by endpoint Flows.</summary>
[<RequireQualifiedAccess>]
module Response =
    /// <summary>Streams a trusted value as JSON through a compiled codec.</summary>
    /// <param name="statusCode">The successful HTTP status code.</param>
    /// <param name="codec">The compiled codec for the trusted output type.</param>
    /// <param name="value">The trusted output value.</param>
    /// <returns>An ASP.NET result that streams the encoded JSON.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>return Response.json 201 Signup.codec signup</code></example>
    let json (statusCode: int) (codec: JsonCodec<'model>) (value: 'model) : IResult =
        SchemaResult.codec codec statusCode value

    /// <summary>Returns an empty response with the supplied status code.</summary>
    /// <param name="statusCode">The successful HTTP status code.</param>
    /// <returns>An empty ASP.NET result.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>return Response.empty 204</code></example>
    let empty (statusCode: int) : IResult =
        Results.StatusCode statusCode

    /// <summary>Returns a plain-text response.</summary>
    /// <param name="statusCode">The successful HTTP status code.</param>
    /// <param name="value">The response text.</param>
    /// <returns>An ASP.NET plain-text result.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>return Response.text 200 "ready"</code></example>
    let text (statusCode: int) (value: string) : IResult =
        Results.Text(value, statusCode = statusCode)

    /// <summary>Returns an HTML response, such as a page or the fragment an htmx request swaps in.</summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="html">The rendered HTML.</param>
    /// <returns>An ASP.NET result with content type <c>text/html; charset=utf-8</c>.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the view is supplied by the application."><code>return Response.html 200 (renderHtml page)</code></example>
    let html (statusCode: int) (html: string) : IResult =
        Results.Text(html, "text/html; charset=utf-8", statusCode = statusCode)

    /// <summary>Returns a redirect to another address.</summary>
    /// <param name="statusCode">The redirect status: 303 after a form post, 302 or 307 for a temporary move, 301 or 308 for a permanent one.</param>
    /// <param name="location">The address sent in the <c>Location</c> header.</param>
    /// <returns>An empty ASP.NET result with the status and <c>Location</c> header.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context."><code>return Response.redirect 303 "/signups"</code></example>
    let redirect (statusCode: int) (location: string) : IResult =
        { new IResult with
            member _.ExecuteAsync(context: HttpContext) =
                context.Response.StatusCode <- statusCode
                context.Response.Headers.Location <- Microsoft.Extensions.Primitives.StringValues location
                Task.CompletedTask }

    /// <summary>Adds a header to a response, replacing a value already set under that name.</summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <param name="response">The response to add the header to.</param>
    /// <returns>The response, sent with the header.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the view is supplied by the application.">
    /// <code>
    /// return
    ///     Response.html 200 fragment
    ///     |> Response.withHeader "HX-Push-Url" address
    ///     |> Response.withHeader "HX-Trigger" "saved"
    /// </code>
    /// </example>
    let withHeader (name: string) (value: string) (response: IResult) : IResult =
        { new IResult with
            member _.ExecuteAsync(context: HttpContext) =
                // The inner result sets its own headers, such as Content-Type or Location, as it runs. Setting this
                // one as the headers are sent lets it win. Callbacks run last-registered first, so the outermost
                // withHeader for a name wins, as in the GenHTTP adapter.
                context.Response.OnStarting(fun () ->
                    context.Response.Headers[name] <- Microsoft.Extensions.Primitives.StringValues value
                    Task.CompletedTask)
                response.ExecuteAsync context }

    /// <summary>Returns a host-native ASP.NET result unchanged.</summary>
    /// <param name="result">The result constructed through ASP.NET APIs.</param>
    /// <returns>The supplied result.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application."><code>return Response.native (Results.Redirect "/login")</code></example>
    let native (result: IResult) : IResult = result

module private EndpointLogging =
    let defaultSink = MicrosoftLogging.fromFactory "Axial.Endpoint"

/// <summary>Per-endpoint options.</summary>
type EndpointOptions =
    { /// <summary>The request logging policy. Custom callbacks run once per request; only the default adapter is cached per factory. <c>None</c> leaves the runtime default; <c>LogSink.none</c> disables logging.</summary>
      LogSink: (ILoggerFactory -> ILogSink) option
      /// <summary>The largest request body, in bytes, this endpoint accepts. <c>None</c> keeps the server's limit (30,000,000 bytes unless Kestrel is configured otherwise). A larger body ends <c>Request.json</c>, <c>Request.form</c> and <c>Request.parseForm</c> with a 413 problem response.</summary>
      /// <remarks>Sets <c>IHttpMaxRequestBodySizeFeature.MaxRequestBodySize</c> for the request, as <c>RequestSizeLimitAttribute</c> does. A server without that feature keeps its own limit.</remarks>
      MaxRequestBodySize: int64 option }
    static member Default = { LogSink = Some EndpointLogging.defaultSink; MaxRequestBodySize = None }

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
    /// <param name="options">Custom log-sink callbacks run once per request. Default caches the Axial.Endpoint Microsoft logging adapter per factory.</param>
    /// <param name="getAppEnvironment">Constructs or resolves the explicit application environment for the current request.</param>
    /// <param name="mapApplicationError">Maps one expected application failure to an ASP.NET result.</param>
    /// <param name="workflow">The complete endpoint Flow to execute.</param>
    /// <returns>A native delegate suitable for <c>MapGet</c>, <c>MapPost</c>, and the other ASP.NET routing methods.</returns>
    /// <example data-livedocs="no-check" reason="Shows the expression in its endpoint-flow context; the surrounding schema, codec, and request are supplied by the application.">
    /// <code>
    /// let endpoint = flowEndpoint AppEnv.fromContext ApiError.toResponse
    /// app.MapPost("/signups", endpoint signupEndpoint)
    /// </code>
    /// </example>
    let flowEndpointWithOptions
        (options: EndpointOptions)
        (getAppEnvironment: HttpContext -> 'app)
        (mapApplicationError: 'error -> IResult)
        (workflow: Flow<HttpEndpointEnv<'app>, EndpointError<'error>, IResult>)
        : System.Func<HttpContext, Task<IResult>> =
        // Only the default adapter is request-independent: Microsoft logging scopes stay ambient.
        // Custom callbacks may capture request-owned state and must run on every request.
        let sinks =
            System.Runtime.CompilerServices.ConditionalWeakTable<ILoggerFactory, ILogSink>()

        System.Func<HttpContext, Task<IResult>>(fun context ->
            task {
                match options.MaxRequestBodySize with
                | Some limit ->
                    match context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() with
                    | null -> ()
                    | feature when feature.IsReadOnly -> ()
                    | feature -> feature.MaxRequestBodySize <- Nullable limit
                | None -> ()

                let environment =
                    { App = getAppEnvironment context
                      Request = context.Request }

                let hosted =
                    match options.LogSink with
                    | Some createSink ->
                        let factory = context.RequestServices.GetRequiredService<ILoggerFactory>()

                        let sink =
                            if obj.ReferenceEquals(createSink, EndpointLogging.defaultSink) then
                                sinks.GetValue(
                                    factory,
                                    System.Runtime.CompilerServices.ConditionalWeakTable<ILoggerFactory, ILogSink>.CreateValueCallback(fun factory ->
                                        createSink factory)
                                )
                            else
                                createSink factory

                        workflow |> Flow.withLogSink sink
                    | None -> workflow
                let! exit = hosted.StartAsTask(environment, cancellationToken = context.RequestAborted)

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
                            let rendered = cause.ToString()
                            return raise (InvalidOperationException($"Endpoint Flow failed with an unsupported composite cause: {rendered}"))
            })

    /// <summary>Runs a request with Microsoft logging in category Axial.Endpoint. Inner scoped sinks override it.</summary>
    let flowEndpoint getAppEnvironment mapApplicationError workflow =
        flowEndpointWithOptions EndpointOptions.Default getAppEnvironment mapApplicationError workflow
