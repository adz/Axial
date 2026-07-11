namespace Axial.Flow.Http

open System
open System.Globalization
open System.Text
open Axial.Flow
#if !FABLE_COMPILER
open System.Net.Http
open System.Threading
open System.Threading.Tasks
#endif

/// Identifies one HTTP request method.
[<RequireQualifiedAccess>]
type Method =
    | Get
    | Head
    | Post
    | Put
    | Patch
    | Delete
    | Options
    | Custom of name: string

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Method =
    /// Returns the canonical uppercase method name. <example><code>Method.name Method.Get</code></example>
    let name = function
        | Method.Get -> "GET"
        | Method.Head -> "HEAD"
        | Method.Post -> "POST"
        | Method.Put -> "PUT"
        | Method.Patch -> "PATCH"
        | Method.Delete -> "DELETE"
        | Method.Options -> "OPTIONS"
        | Method.Custom name -> name.ToUpperInvariant()

/// Supplies the request payload and its media type.
[<RequireQualifiedAccess>]
type RequestBody =
    | Empty
    | Text of content: string * contentType: string
    | Bytes of content: byte array * contentType: string
    | Form of fields: (string * string) list

/// Decides which response status codes count as success for a request.
[<RequireQualifiedAccess>]
type StatusExpectation =
    /// Any status in the 200-299 range succeeds. This is the default.
    | Success
    /// Only the listed statuses succeed.
    | Statuses of Set<int>
    /// Every status succeeds; the caller inspects the status explicitly.
    | Any

/// One header or query parameter with an optional redaction flag for diagnostics.
type internal Parameter =
    { Name: string
      Value: string
      Secret: bool }

/// An immutable, fully described HTTP request.
type HttpRequest =
    internal
        { Method: Method
          Url: string
          DisplayUrl: string
          Query: Parameter list
          Headers: Parameter list
          Body: RequestBody
          Timeout: TimeSpan option
          Expectation: StatusExpectation }

/// A redacted, serializable description of a request that would be sent.
type RequestPlan =
    { Method: string
      Url: string
      Headers: (string * string) list
      Body: string
      Timeout: TimeSpan option
      Expectation: string }

/// The complete response transcript for one HTTP exchange.
type HttpResponse =
    { /// The numeric response status code.
      StatusCode: int
      /// The reason phrase supplied by the server, or an empty string.
      ReasonPhrase: string
      /// Response and content headers, in arrival order.
      Headers: (string * string) list
      /// The exact response body bytes.
      Body: byte array
      /// The response body decoded with the response charset (UTF-8 when unspecified).
      Text: string
      /// The redacted request line, such as <c>GET https://api.example.com/users/***</c>.
      Request: string
      /// When the request started.
      StartedAt: DateTimeOffset
      /// Total exchange duration including body download.
      Duration: TimeSpan }

/// A recoverable HTTP transport, timeout, status, or decoding failure.
[<RequireQualifiedAccess>]
type HttpError =
    /// The request could not be constructed, for example from a malformed URL.
    | InvalidRequest of message: string
    /// The connection could not be established or was dropped before a response arrived.
    | ConnectionFailed of request: string * message: string
    /// The per-request timeout elapsed before the response completed.
    | TimedOut of request: string * timeout: TimeSpan
    /// The workflow was canceled while the request was in flight.
    | Canceled of message: string
    /// The response arrived with a status outside the request's expectation.
    | Status of response: HttpResponse
    /// The response body could not be decoded into the requested value.
    | DecodeFailed of message: string * response: HttpResponse

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module HttpError =
    /// Formats an HTTP error with its redacted request context.
    /// <example><code>error |&gt; HttpError.describe</code></example>
    let describe = function
        | HttpError.InvalidRequest message -> $"Invalid HTTP request: {message}"
        | HttpError.ConnectionFailed(request, message) -> $"Could not reach '{request}': {message}"
        | HttpError.TimedOut(request, timeout) ->
            let seconds = timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)
            $"'{request}' did not complete within {seconds} seconds."
        | HttpError.Canceled message -> $"HTTP request was canceled: {message}"
        | HttpError.Status response ->
            let preview = if response.Text.Length > 512 then response.Text.Substring(0, 512) + "…" else response.Text
            let detail = if preview = "" then "" else Environment.NewLine + preview
            $"'{response.Request}' returned {response.StatusCode} {response.ReasonPhrase}.{detail}"
        | HttpError.DecodeFailed(message, response) -> $"Could not decode the response from '{response.Request}': {message}"

    /// Returns the response transcript when the error carries one.
    /// <example><code>HttpError.tryResponse error</code></example>
    let tryResponse = function
        | HttpError.Status response
        | HttpError.DecodeFailed(_, response) -> Some response
        | _ -> None

    /// Indicates whether retrying the same request could plausibly succeed.
    /// Connection failures, timeouts, and 408/429/5xx statuses are transient.
    /// <example><code>if HttpError.isTransient error then retry ()</code></example>
    let isTransient = function
        | HttpError.ConnectionFailed _
        | HttpError.TimedOut _ -> true
        | HttpError.Status response ->
            response.StatusCode = 408 || response.StatusCode = 429 || response.StatusCode >= 500
        | _ -> false

    /// Builds a retry policy with exponential backoff that retries only transient HTTP errors.
    /// <example><code>workflow |&gt; Flow.Runtime.retry (HttpError.transientPolicy 4 (TimeSpan.FromMilliseconds 200.0))</code></example>
    let transientPolicy (maxAttempts: int) (baseDelay: TimeSpan) : RetryPolicy<HttpError> =
        { MaxAttempts = maxAttempts
          Delay = fun attempt -> TimeSpan.FromTicks(baseDelay.Ticks <<< min 16 (attempt - 1))
          ShouldRetry = isTransient }

/// Sends fully described HTTP requests for a concrete host platform.
type IHttp =
    abstract Send : request: HttpRequest * cancellationToken: System.Threading.CancellationToken -> Async<Result<HttpResponse, HttpError>>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Request =
    let internal formatValue (value: obj) =
        match value with
        | null -> ""
        | :? IFormattable as formattable -> formattable.ToString(null, CultureInfo.InvariantCulture)
        | _ -> string value

    /// Creates a request with the supplied method and already-formed URL.
    /// <example><code>Request.create Method.Get "https://api.example.com/users"</code></example>
    let create method url : HttpRequest =
        if String.IsNullOrWhiteSpace url then invalidArg (nameof url) "A request URL cannot be empty."
        { Method = method; Url = url; DisplayUrl = url
          Query = []; Headers = []
          Body = RequestBody.Empty; Timeout = None
          Expectation = StatusExpectation.Success }

    /// Returns the request method. <example><code>Request.method request</code></example>
    let method (request: HttpRequest) = request.Method
    /// Returns the real request URL without appended query parameters. <example><code>Request.url request</code></example>
    let url (request: HttpRequest) = request.Url
    /// Returns query parameters as name-value pairs. <example><code>Request.queryParameters request</code></example>
    let queryParameters (request: HttpRequest) = request.Query |> List.map (fun p -> p.Name, p.Value)
    /// Returns headers as name-value pairs. <example><code>Request.headerValues request</code></example>
    let headerValues (request: HttpRequest) = request.Headers |> List.map (fun p -> p.Name, p.Value)
    /// Returns the request body. <example><code>Request.body request</code></example>
    let body (request: HttpRequest) = request.Body
    /// Returns the per-request timeout. <example><code>Request.tryTimeout request</code></example>
    let tryTimeout (request: HttpRequest) = request.Timeout
    /// Returns the success expectation. <example><code>Request.expectation request</code></example>
    let expectation (request: HttpRequest) = request.Expectation

    /// Appends one URL-encoded query parameter.
    /// <example><code>request |&gt; Request.query "page" 2</code></example>
    let query (name: string) (value: obj) (request: HttpRequest) =
        { request with Query = request.Query @ [ { Name = name; Value = formatValue value; Secret = false } ] }

    /// Appends a query parameter whose value is replaced with <c>***</c> in plans and error transcripts.
    /// <example><code>request |&gt; Request.secretQuery "api_key" key</code></example>
    let secretQuery (name: string) (value: obj) (request: HttpRequest) =
        { request with Query = request.Query @ [ { Name = name; Value = formatValue value; Secret = true } ] }

    /// Appends one request header.
    /// <example><code>request |&gt; Request.header "Accept" "application/json"</code></example>
    let header (name: string) (value: string) (request: HttpRequest) =
        { request with Headers = request.Headers @ [ { Name = name; Value = value; Secret = false } ] }

    /// Appends a header whose value is replaced with <c>***</c> in plans and error transcripts.
    /// <example><code>request |&gt; Request.secretHeader "X-Api-Key" key</code></example>
    let secretHeader (name: string) (value: string) (request: HttpRequest) =
        { request with Headers = request.Headers @ [ { Name = name; Value = value; Secret = true } ] }

    /// Adds a bearer-token Authorization header. The token is always redacted in diagnostics.
    /// <example><code>request |&gt; Request.bearer token</code></example>
    let bearer (token: string) (request: HttpRequest) =
        secretHeader "Authorization" $"Bearer {token}" request

    /// Adds a basic-auth Authorization header. The credentials are always redacted in diagnostics.
    /// <example><code>request |&gt; Request.basicAuth user password</code></example>
    let basicAuth (user: string) (password: string) (request: HttpRequest) =
        let credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes $"{user}:{password}")
        secretHeader "Authorization" $"Basic {credentials}" request

    /// Sets the Accept header. <example><code>request |&gt; Request.accept "application/json"</code></example>
    let accept (mediaType: string) (request: HttpRequest) = header "Accept" mediaType request
    /// Sets the Accept header to <c>application/json</c>. <example><code>request |&gt; Request.acceptJson</code></example>
    let acceptJson (request: HttpRequest) = accept "application/json" request
    /// Sets the User-Agent header. <example><code>request |&gt; Request.userAgent "axial-app/1.0"</code></example>
    let userAgent (value: string) (request: HttpRequest) = header "User-Agent" value request

    /// Sets a per-request timeout enforced by the live service.
    /// <example><code>request |&gt; Request.timeout (TimeSpan.FromSeconds 5.0)</code></example>
    let timeout (value: TimeSpan) (request: HttpRequest) =
        if value <= TimeSpan.Zero then invalidArg (nameof value) "A request timeout must be positive."
        { request with Timeout = Some value }

    /// Sends a plain-text body. <example><code>request |&gt; Request.textBody "hello"</code></example>
    let textBody (content: string) (request: HttpRequest) =
        { request with Body = RequestBody.Text(content, "text/plain") }

    /// Sends an already-serialized JSON body with the <c>application/json</c> content type.
    /// <example><code>request |&gt; Request.jsonBody """{"name":"Ada"}"""</code></example>
    let jsonBody (json: string) (request: HttpRequest) =
        { request with Body = RequestBody.Text(json, "application/json") }

    /// Encodes a value with the supplied serializer and sends it as JSON.
    /// <example><code>request |&gt; Request.jsonBodyWith (Json.serialize codec) user</code></example>
    let jsonBodyWith (encode: 'value -> string) (value: 'value) (request: HttpRequest) =
        jsonBody (encode value) request

    /// Sends raw bytes with an explicit content type.
    /// <example><code>request |&gt; Request.bytesBody "application/octet-stream" payload</code></example>
    let bytesBody (contentType: string) (content: byte array) (request: HttpRequest) =
        { request with Body = RequestBody.Bytes(content, contentType) }

    /// Sends URL-encoded form fields. <example><code>request |&gt; Request.formBody [ "q", "axial" ]</code></example>
    let formBody (fields: (string * string) list) (request: HttpRequest) =
        { request with Body = RequestBody.Form fields }

    /// Replaces the statuses treated as success.
    /// <example><code>request |&gt; Request.expect [ 200; 404 ]</code></example>
    let expect (statuses: int seq) (request: HttpRequest) =
        let codes = Set.ofSeq statuses
        if Set.isEmpty codes then invalidArg (nameof statuses) "At least one expected status code is required."
        { request with Expectation = StatusExpectation.Statuses codes }

    /// Treats every status as success so the caller can branch on the code explicitly.
    /// <example><code>request |&gt; Request.expectAny</code></example>
    let expectAny (request: HttpRequest) = { request with Expectation = StatusExpectation.Any }

    let internal encodedQuery (parameters: Parameter list) (redacted: bool) =
        parameters
        |> List.map (fun parameter ->
            let value = if redacted && parameter.Secret then "***" else Uri.EscapeDataString parameter.Value
            Uri.EscapeDataString parameter.Name + "=" + value)
        |> String.concat "&"

    let internal fullUrl (request: HttpRequest) =
        if List.isEmpty request.Query then request.Url
        else
            let separator = if request.Url.Contains "?" then "&" else "?"
            request.Url + separator + encodedQuery request.Query false

    /// Renders the redacted request line used in error transcripts.
    /// <example><code>Request.render request</code></example>
    let render (request: HttpRequest) =
        let url =
            if List.isEmpty request.Query then request.DisplayUrl
            else
                let separator = if request.DisplayUrl.Contains "?" then "&" else "?"
                request.DisplayUrl + separator + encodedQuery request.Query true
        $"{Method.name request.Method} {url}"

    /// Returns a redacted request plan without sending anything.
    /// <example><code>Request.plan request</code></example>
    let plan (request: HttpRequest) : RequestPlan =
        let body =
            match request.Body with
            | RequestBody.Empty -> "empty"
            | RequestBody.Text(content, contentType) -> $"{contentType} ({content.Length} characters)"
            | RequestBody.Bytes(content, contentType) -> $"{contentType} ({content.Length} bytes)"
            | RequestBody.Form fields -> $"form ({fields.Length} fields)"
        let expectation =
            match request.Expectation with
            | StatusExpectation.Success -> "2xx"
            | StatusExpectation.Statuses codes -> codes |> Seq.map string |> String.concat ", "
            | StatusExpectation.Any -> "any status"
        { Method = Method.name request.Method
          Url = (render request).Substring(Method.name request.Method |> String.length |> (+) 1)
          Headers = request.Headers |> List.map (fun p -> p.Name, (if p.Secret then "***" else p.Value))
          Body = body
          Timeout = request.Timeout
          Expectation = expectation }

    let internal succeeded (request: HttpRequest) (statusCode: int) =
        match request.Expectation with
        | StatusExpectation.Success -> statusCode >= 200 && statusCode <= 299
        | StatusExpectation.Statuses codes -> codes.Contains statusCode
        | StatusExpectation.Any -> true

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Response =
    /// Returns the response body text. <example><code>response |&gt; Response.text</code></example>
    let text (response: HttpResponse) = response.Text
    /// Returns the exact response body bytes. <example><code>response |&gt; Response.bytes</code></example>
    let bytes (response: HttpResponse) = response.Body
    /// Returns the response status code. <example><code>response |&gt; Response.statusCode</code></example>
    let statusCode (response: HttpResponse) = response.StatusCode
    /// Finds the first header with the given case-insensitive name.
    /// <example><code>response |&gt; Response.tryHeader "ETag"</code></example>
    let tryHeader (name: string) (response: HttpResponse) =
        response.Headers
        |> List.tryPick (fun (candidate, value) ->
            if String.Equals(candidate, name, StringComparison.OrdinalIgnoreCase) then Some value else None)

    /// Decodes the response body with the supplied decoder, mapping failure to <c>HttpError.DecodeFailed</c>.
    /// <example><code>response |&gt; Response.json (Json.deserializeResult codec)</code></example>
    let json (decode: string -> Result<'value, string>) (response: HttpResponse) : Result<'value, HttpError> =
        match decode response.Text with
        | Ok value -> Ok value
        | Error message -> Error(HttpError.DecodeFailed(message, response))

    /// Creates a synthetic response transcript, primarily for test fakes.
    /// <example><code>Response.create 200 """{"ok":true}"""</code></example>
    let create (status: int) (bodyText: string) : HttpResponse =
        { StatusCode = status
          ReasonPhrase = ""
          Headers = []
          Body = Encoding.UTF8.GetBytes bodyText
          Text = bodyText
          Request = ""
          StartedAt = DateTimeOffset.UtcNow
          Duration = TimeSpan.Zero }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Http =
    /// Creates a GET request. <example><code>Http.get "https://api.example.com/users"</code></example>
    let get url = Request.create Method.Get url
    /// Creates a HEAD request. <example><code>Http.head "https://api.example.com/users"</code></example>
    let head url = Request.create Method.Head url
    /// Creates a POST request. <example><code>Http.post "https://api.example.com/users"</code></example>
    let post url = Request.create Method.Post url
    /// Creates a PUT request. <example><code>Http.put "https://api.example.com/users/1"</code></example>
    let put url = Request.create Method.Put url
    /// Creates a PATCH request. <example><code>Http.patch "https://api.example.com/users/1"</code></example>
    let patch url = Request.create Method.Patch url
    /// Creates a DELETE request. <example><code>Http.delete "https://api.example.com/users/1"</code></example>
    let delete url = Request.create Method.Delete url

    /// Sends a request and returns the transcript without interpreting the status expectation.
    /// <example><code>request |&gt; Http.sendResult</code></example>
    let sendResult<'env when 'env :> IHas<IHttp>> (request: HttpRequest) : Flow<'env, HttpError, HttpResponse> =
        flow {
            let! service = Service<IHttp>.get()
            let! cancellationToken = Flow.Runtime.cancellationToken
            let! outcome: Result<HttpResponse, HttpError> = service.Send(request, cancellationToken)
            return! outcome
        }

    /// Sends a request and fails with <c>HttpError.Status</c> when the response is outside the expectation.
    /// <example><code>request |&gt; Http.send</code></example>
    let send<'env when 'env :> IHas<IHttp>> (request: HttpRequest) : Flow<'env, HttpError, HttpResponse> =
        flow {
            let! response = sendResult request
            if Request.succeeded request response.StatusCode then return response
            else return! Flow.fail (HttpError.Status response)
        }

    /// Sends a request and returns the response body text.
    /// <example><code>Http.get url |&gt; Request.bearer token |&gt; Http.text</code></example>
    let text<'env when 'env :> IHas<IHttp>> (request: HttpRequest) : Flow<'env, HttpError, string> =
        send request |> Flow.map Response.text

    /// Sends a request and returns the exact response body bytes.
    /// <example><code>Http.get url |&gt; Http.bytes</code></example>
    let bytes<'env when 'env :> IHas<IHttp>> (request: HttpRequest) : Flow<'env, HttpError, byte array> =
        send request |> Flow.map Response.bytes

    /// Sends a request and decodes the JSON response body with the supplied decoder.
    /// <example><code>Http.get url |&gt; Request.acceptJson |&gt; Http.json (Json.deserializeResult codec)</code></example>
    let json<'env, 'value when 'env :> IHas<IHttp>>
        (decode: string -> Result<'value, string>)
        (request: HttpRequest)
        : Flow<'env, HttpError, 'value> =
        flow {
            let! response = send request
            return! Response.json decode response
        }

    /// Sends a GET request and returns the response body, mirroring <c>HttpClient.GetStringAsync</c>.
    /// <example><code>Http.getString "https://example.com"</code></example>
    let getString<'env when 'env :> IHas<IHttp>> (url: string) : Flow<'env, HttpError, string> =
        get url |> text

    /// Sends a GET request and returns the body bytes, mirroring <c>HttpClient.GetByteArrayAsync</c>.
    /// <example><code>Http.getBytes "https://example.com/logo.png"</code></example>
    let getBytes<'env when 'env :> IHas<IHttp>> (url: string) : Flow<'env, HttpError, byte array> =
        get url |> bytes

    /// Sends a GET request and decodes the JSON response.
    /// <example><code>Http.getJson (Json.deserializeResult codec) "https://api.example.com/users/1"</code></example>
    let getJson<'env, 'value when 'env :> IHas<IHttp>>
        (decode: string -> Result<'value, string>)
        (url: string)
        : Flow<'env, HttpError, 'value> =
        get url |> Request.acceptJson |> json decode

    /// Sends a POST request with a text body, mirroring <c>HttpClient.PostAsync</c> with string content.
    /// <example><code>Http.postString "https://example.com/echo" "hello"</code></example>
    let postString<'env when 'env :> IHas<IHttp>> (url: string) (content: string) : Flow<'env, HttpError, HttpResponse> =
        post url |> Request.textBody content |> send

    /// Encodes a value as JSON, POSTs it, and decodes the JSON response.
    /// <example><code>Http.postJson (Json.serialize codec) (Json.deserializeResult codec) url user</code></example>
    let postJson<'env, 'input, 'value when 'env :> IHas<IHttp>>
        (encode: 'input -> string)
        (decode: string -> Result<'value, string>)
        (url: string)
        (value: 'input)
        : Flow<'env, HttpError, 'value> =
        post url |> Request.jsonBodyWith encode value |> Request.acceptJson |> json decode

    /// Retries a workflow on transient HTTP errors with exponential backoff.
    /// Permanent failures such as 404 or decode errors are never retried.
    /// <example><code>Http.getJson decode url |&gt; Http.retryTransient 4 (TimeSpan.FromMilliseconds 200.0)</code></example>
    let retryTransient
        (maxAttempts: int)
        (baseDelay: TimeSpan)
        (workflow: Flow<'env, HttpError, 'value>)
        : Flow<'env, HttpError, 'value> =
        workflow |> Flow.Runtime.retry (HttpError.transientPolicy maxAttempts baseDelay)

#if !FABLE_COMPILER
    let private decodeBody (contentType: string option) (body: byte array) =
        let encoding =
            contentType
            |> Option.bind (fun value ->
                value.Split(';')
                |> Array.tryPick (fun part ->
                    let part = part.Trim()
                    if part.StartsWith("charset=", StringComparison.OrdinalIgnoreCase) then
                        try Some(Encoding.GetEncoding(part.Substring(8).Trim('"'))) with _ -> None
                    else None))
            |> Option.defaultValue Encoding.UTF8
        encoding.GetString body

    let private toMessage (request: HttpRequest) =
        let message = new HttpRequestMessage(HttpMethod(Method.name request.Method), Request.fullUrl request)
        match request.Body with
        | RequestBody.Empty -> ()
        | RequestBody.Text(content, contentType) ->
            message.Content <- new StringContent(content, Encoding.UTF8, contentType)
        | RequestBody.Bytes(content, contentType) ->
            let body = new ByteArrayContent(content)
            body.Headers.ContentType <- Headers.MediaTypeHeaderValue.Parse contentType
            message.Content <- body
        | RequestBody.Form fields ->
            message.Content <- new FormUrlEncodedContent(fields |> List.map Collections.Generic.KeyValuePair)
        for parameter in request.Headers do
            if not (message.Headers.TryAddWithoutValidation(parameter.Name, parameter.Value)) then
                match message.Content with
                | null -> ()
                | content -> content.Headers.TryAddWithoutValidation(parameter.Name, parameter.Value) |> ignore
        message

    /// Creates a live HTTP service backed by <see cref="T:System.Net.Http.HttpClient" />.
    /// <example><code>Http.live (new HttpClient())</code></example>
    let live (client: HttpClient) : IHttp =
        { new IHttp with
            member _.Send(request, cancellationToken) =
                async {
                    return! task {
                        let display = Request.render request
                        let startedAt = DateTimeOffset.UtcNow
                        use timeoutSource = CancellationTokenSource.CreateLinkedTokenSource cancellationToken
                        request.Timeout |> Option.iter timeoutSource.CancelAfter
                        try
                            use message = toMessage request
                            use! response = client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token)
                            let! body = response.Content.ReadAsByteArrayAsync()
                            let contentType =
                                match response.Content.Headers.ContentType with
                                | null -> None
                                | value -> Some(value.ToString())
                            let headers =
                                [ for pair in response.Headers do
                                    for value in pair.Value do pair.Key, value
                                  for pair in response.Content.Headers do
                                    for value in pair.Value do pair.Key, value ]
                            return Ok
                                { StatusCode = int response.StatusCode
                                  ReasonPhrase = (match response.ReasonPhrase with null -> "" | reason -> reason)
                                  Headers = headers
                                  Body = body
                                  Text = decodeBody contentType body
                                  Request = display
                                  StartedAt = startedAt
                                  Duration = DateTimeOffset.UtcNow - startedAt }
                        with
                        | :? OperationCanceledException when cancellationToken.IsCancellationRequested ->
                            return Error(HttpError.Canceled display)
                        | :? OperationCanceledException ->
                            let timeout = request.Timeout |> Option.defaultValue client.Timeout
                            return Error(HttpError.TimedOut(display, timeout))
                        | :? HttpRequestException as error ->
                            let detail =
                                match error.InnerException with
                                | null -> error.Message
                                | inner -> $"{error.Message} {inner.Message}"
                            return Error(HttpError.ConnectionFailed(display, detail))
                        | :? UriFormatException as error ->
                            return Error(HttpError.InvalidRequest error.Message)
                        | :? InvalidOperationException as error ->
                            return Error(HttpError.InvalidRequest error.Message)
                    } |> Async.AwaitTask
                } }

    /// Builds a live HTTP service as a layer.
    /// <example><code>Http.layer (new HttpClient())</code></example>
    let layer (client: HttpClient) : Layer<unit, Never, IHttp> =
        Layer.succeed (live client)
#endif

module DSL =
    /// Marks an interpolated URL value for redaction in plans and transcripts.
    [<Sealed>]
    type SecretValue internal (value: obj) =
        member internal _.Value = value

    /// Marks an interpolated URL value for diagnostic redaction.
    /// <example><code>GET $"https://api.example.com/users?key={secret apiKey}"</code></example>
    let secret value = SecretValue(box value)

    let private buildUrl (template: FormattableString) =
        let values = template.GetArguments()
        let format = template.Format
        let actual = StringBuilder()
        let display = StringBuilder()
        let mutable index = 0
        while index < format.Length do
            if format[index] = '{' && index + 1 < format.Length && format[index + 1] = '{' then
                actual.Append '{' |> ignore; display.Append '{' |> ignore; index <- index + 2
            elif format[index] = '}' && index + 1 < format.Length && format[index + 1] = '}' then
                actual.Append '}' |> ignore; display.Append '}' |> ignore; index <- index + 2
            elif format[index] = '{' then
                let closing = format.IndexOf('}', index + 1)
                if closing < 0 then invalidArg (nameof template) "Unclosed URL interpolation hole."
                let descriptor = format.Substring(index + 1, closing - index - 1)
                let separator = descriptor.IndexOfAny [| ','; ':' |]
                let indexText = if separator < 0 then descriptor else descriptor.Substring(0, separator)
                let valueIndex = Int32.Parse(indexText, CultureInfo.InvariantCulture)
                if valueIndex < 0 || valueIndex >= values.Length then invalidArg (nameof template) "URL interpolation index is out of range."
                let encoded, secret =
                    match values[valueIndex] with
                    | :? SecretValue as secretValue -> Uri.EscapeDataString(Request.formatValue secretValue.Value), true
                    | value -> Uri.EscapeDataString(Request.formatValue value), false
                actual.Append encoded |> ignore
                display.Append(if secret then "***" else encoded) |> ignore
                index <- closing + 1
            else
                actual.Append format[index] |> ignore
                display.Append format[index] |> ignore
                index <- index + 1
        actual.ToString(), display.ToString()

    let private request method (template: FormattableString) =
        let url, display = buildUrl template
        { Request.create method url with DisplayUrl = display }

    /// Creates a GET request from an interpolated URL. Every hole is URL-encoded as one value.
    /// <example><code>GET $"https://api.example.com/users/{userId}"</code></example>
    let GET template = request Method.Get template
    /// Creates a HEAD request from an interpolated URL with encoded holes.
    let HEAD template = request Method.Head template
    /// Creates a POST request from an interpolated URL with encoded holes.
    /// <example><code>POST $"https://api.example.com/users" |&gt; jsonBody payload</code></example>
    let POST template = request Method.Post template
    /// Creates a PUT request from an interpolated URL with encoded holes.
    let PUT template = request Method.Put template
    /// Creates a PATCH request from an interpolated URL with encoded holes.
    let PATCH template = request Method.Patch template
    /// Creates a DELETE request from an interpolated URL with encoded holes.
    let DELETE template = request Method.Delete template

    /// Appends a URL-encoded query parameter. <example><code>GET $"{root}/search" |&gt; query "q" term</code></example>
    let query name value request = Request.query name value request
    /// Appends a redacted query parameter. <example><code>request |&gt; secretQuery "api_key" key</code></example>
    let secretQuery name value request = Request.secretQuery name value request
    /// Appends one request header. <example><code>request |&gt; header "Accept" "text/csv"</code></example>
    let header name value request = Request.header name value request
    /// Adds a redacted bearer-token Authorization header. <example><code>request |&gt; bearer token</code></example>
    let bearer token request = Request.bearer token request
    /// Adds a redacted basic-auth Authorization header. <example><code>request |&gt; basicAuth user password</code></example>
    let basicAuth user password request = Request.basicAuth user password request
    /// Sets a per-request timeout. <example><code>request |&gt; timeout (TimeSpan.FromSeconds 5.0)</code></example>
    let timeout value request = Request.timeout value request
    /// Sends an already-serialized JSON body. <example><code>request |&gt; jsonBody """{"name":"Ada"}"""</code></example>
    let jsonBody json request = Request.jsonBody json request
    /// Encodes and sends a JSON body. <example><code>request |&gt; jsonBodyOf (Json.serialize codec) user</code></example>
    let jsonBodyOf encode value request = Request.jsonBodyWith encode value request
    /// Sends a plain-text body. <example><code>request |&gt; textBody "hello"</code></example>
    let textBody content request = Request.textBody content request
    /// Sends URL-encoded form fields. <example><code>request |&gt; formBody [ "q", "axial" ]</code></example>
    let formBody fields request = Request.formBody fields request
    /// Replaces the statuses treated as success. <example><code>request |&gt; expect [ 200; 404 ]</code></example>
    let expect statuses request = Request.expect statuses request
    /// Treats every status as success. <example><code>request |&gt; expectAny</code></example>
    let expectAny request = Request.expectAny request

    /// Sends the request and returns the full transcript, failing on unexpected statuses.
    /// <example><code>GET $"{root}/users" |&gt; fetch</code></example>
    let inline fetch request = Http.send request
    /// Sends the request and returns the body text. <example><code>GET $"{root}/readme" |&gt; fetchText</code></example>
    let inline fetchText request = Http.text request
    /// Sends the request and returns the body bytes. <example><code>GET $"{root}/logo.png" |&gt; fetchBytes</code></example>
    let inline fetchBytes request = Http.bytes request
    /// Sends the request and decodes the JSON response.
    /// <example><code>GET $"{root}/users/{id}" |&gt; fetchJson (Json.deserializeResult codec)</code></example>
    let inline fetchJson decode request = Http.json decode request
    /// Retries transient failures with exponential backoff.
    /// <example><code>GET $"{root}/users" |&gt; fetchJson decode |&gt; withRetries 4</code></example>
    let withRetries maxAttempts workflow = Http.retryTransient maxAttempts (TimeSpan.FromMilliseconds 200.0) workflow
