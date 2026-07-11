---
title: "Services Http"
weight: 40
type: docs
---

This page shows the HTTP client service package. Immutable `HttpRequest` values carry the method, encoded URL, headers, body, timeout, and status expectation; `Http.send` converts a request through the explicit `IHttp` capability and reports connection, timeout, status, and decode failures through `HttpError` with redacted request transcripts. The `DSL` module adds interpolated URL builders and terminal fetch verbs for concise call sites.

## Model

- [`Flow.Http.Method`](./t-flow-http-method.md):  Identifies one HTTP request method.
- [`Flow.Http.RequestBody`](./t-flow-http-requestbody.md):  Supplies the request payload and its media type.
- [`Flow.Http.StatusExpectation`](./t-flow-http-statusexpectation.md):  Decides which response status codes count as success for a request.
- [`Flow.Http.HttpRequest`](./t-flow-http-httprequest.md):  An immutable, fully described HTTP request.
- [`Flow.Http.RequestPlan`](./t-flow-http-requestplan.md):  A redacted, serializable description of a request that would be sent.
- [`Flow.Http.HttpResponse`](./t-flow-http-httpresponse.md):  The complete response transcript for one HTTP exchange.
- [`Flow.Http.HttpError`](./t-flow-http-httperror.md):  A recoverable HTTP transport, timeout, status, or decoding failure.

## Service

- [`Flow.Http.IHttp`](./t-flow-http-ihttp.md):  Sends fully described HTTP requests for a concrete host platform.

## Errors

- [`Flow.Http.HttpError.describe`](./m-flow-http-httperror-describe.md):  Formats an HTTP error with its redacted request context.
 <example><code>error |&gt; HttpError.describe</code></example>
- [`Flow.Http.HttpError.tryResponse`](./m-flow-http-httperror-tryresponse.md):  Returns the response transcript when the error carries one.
 <example><code>HttpError.tryResponse error</code></example>
- [`Flow.Http.HttpError.isTransient`](./m-flow-http-httperror-istransient.md):  Indicates whether retrying the same request could plausibly succeed.
 Connection failures, timeouts, and 408/429/5xx statuses are transient.
 <example><code>if HttpError.isTransient error then retry ()</code></example>
- [`Flow.Http.HttpError.transientPolicy`](./m-flow-http-httperror-transientpolicy.md):  Builds a retry policy with exponential backoff that retries only transient HTTP errors.
 <example><code>workflow |&gt; Flow.Runtime.retry (HttpError.transientPolicy 4 (TimeSpan.FromMilliseconds 200.0))</code></example>

## Request building

- [`Flow.Http.Request.create`](./m-flow-http-request-create.md):  Creates a request with the supplied method and already-formed URL.
 <example><code>Request.create Method.Get "https://api.example.com/users"</code></example>
- [`Flow.Http.Request.query`](./m-flow-http-request-query.md):  Appends one URL-encoded query parameter.
 <example><code>request |&gt; Request.query "page" 2</code></example>
- [`Flow.Http.Request.secretQuery`](./m-flow-http-request-secretquery.md):  Appends a query parameter whose value is replaced with <c>***</c> in plans and error transcripts.
 <example><code>request |&gt; Request.secretQuery "api_key" key</code></example>
- [`Flow.Http.Request.header`](./m-flow-http-request-header.md):  Appends one request header.
 <example><code>request |&gt; Request.header "Accept" "application/json"</code></example>
- [`Flow.Http.Request.secretHeader`](./m-flow-http-request-secretheader.md):  Appends a header whose value is replaced with <c>***</c> in plans and error transcripts.
 <example><code>request |&gt; Request.secretHeader "X-Api-Key" key</code></example>
- [`Flow.Http.Request.bearer`](./m-flow-http-request-bearer.md):  Adds a bearer-token Authorization header. The token is always redacted in diagnostics.
 <example><code>request |&gt; Request.bearer token</code></example>
- [`Flow.Http.Request.basicAuth`](./m-flow-http-request-basicauth.md):  Adds a basic-auth Authorization header. The credentials are always redacted in diagnostics.
 <example><code>request |&gt; Request.basicAuth user password</code></example>
- [`Flow.Http.Request.accept`](./m-flow-http-request-accept.md):  Sets the Accept header. <example><code>request |&gt; Request.accept "application/json"</code></example>
- [`Flow.Http.Request.acceptJson`](./m-flow-http-request-acceptjson.md):  Sets the Accept header to <c>application/json</c>. <example><code>request |&gt; Request.acceptJson</code></example>
- [`Flow.Http.Request.userAgent`](./m-flow-http-request-useragent.md):  Sets the User-Agent header. <example><code>request |&gt; Request.userAgent "axial-app/1.0"</code></example>
- [`Flow.Http.Request.timeout`](./m-flow-http-request-timeout.md):  Sets a per-request timeout enforced by the live service.
 <example><code>request |&gt; Request.timeout (TimeSpan.FromSeconds 5.0)</code></example>
- [`Flow.Http.Request.textBody`](./m-flow-http-request-textbody.md):  Sends a plain-text body. <example><code>request |&gt; Request.textBody "hello"</code></example>
- [`Flow.Http.Request.jsonBody`](./m-flow-http-request-jsonbody.md):  Sends an already-serialized JSON body with the <c>application/json</c> content type.
 <example><code>request |&gt; Request.jsonBody """{"name":"Ada"}"""</code></example>
- [`Flow.Http.Request.jsonBodyWith`](./m-flow-http-request-jsonbodywith.md):  Encodes a value with the supplied serializer and sends it as JSON.
 <example><code>request |&gt; Request.jsonBodyWith (Json.serialize codec) user</code></example>
- [`Flow.Http.Request.bytesBody`](./m-flow-http-request-bytesbody.md):  Sends raw bytes with an explicit content type.
 <example><code>request |&gt; Request.bytesBody "application/octet-stream" payload</code></example>
- [`Flow.Http.Request.formBody`](./m-flow-http-request-formbody.md):  Sends URL-encoded form fields. <example><code>request |&gt; Request.formBody [ "q", "axial" ]</code></example>
- [`Flow.Http.Request.expect`](./m-flow-http-request-expect.md):  Replaces the statuses treated as success.
 <example><code>request |&gt; Request.expect [ 200; 404 ]</code></example>
- [`Flow.Http.Request.expectAny`](./m-flow-http-request-expectany.md):  Treats every status as success so the caller can branch on the code explicitly.
 <example><code>request |&gt; Request.expectAny</code></example>
- [`Flow.Http.Request.render`](./m-flow-http-request-render.md):  Renders the redacted request line used in error transcripts.
 <example><code>Request.render request</code></example>
- [`Flow.Http.Request.plan`](./m-flow-http-request-plan.md):  Returns a redacted request plan without sending anything.
 <example><code>Request.plan request</code></example>

## Responses

- [`Flow.Http.Response.text`](./m-flow-http-response-text.md):  Returns the response body text. <example><code>response |&gt; Response.text</code></example>
- [`Flow.Http.Response.bytes`](./m-flow-http-response-bytes.md):  Returns the exact response body bytes. <example><code>response |&gt; Response.bytes</code></example>
- [`Flow.Http.Response.statusCode`](./m-flow-http-response-statuscode.md):  Returns the response status code. <example><code>response |&gt; Response.statusCode</code></example>
- [`Flow.Http.Response.tryHeader`](./m-flow-http-response-tryheader.md):  Finds the first header with the given case-insensitive name.
 <example><code>response |&gt; Response.tryHeader "ETag"</code></example>
- [`Flow.Http.Response.json`](./m-flow-http-response-json.md):  Decodes the response body with the supplied decoder, mapping failure to <c>HttpError.DecodeFailed</c>.
 <example><code>response |&gt; Response.json (Json.deserializeResult codec)</code></example>
- [`Flow.Http.Response.create`](./m-flow-http-response-create.md):  Creates a synthetic response transcript, primarily for test fakes.
 <example><code>Response.create 200 """{"ok":true}"""</code></example>

## Execution

- [`Flow.Http.get`](./m-flow-http-http-get.md):  Creates a GET request. <example><code>Http.get "https://api.example.com/users"</code></example>
- [`Flow.Http.head`](./m-flow-http-http-head.md):  Creates a HEAD request. <example><code>Http.head "https://api.example.com/users"</code></example>
- [`Flow.Http.post`](./m-flow-http-http-post.md):  Creates a POST request. <example><code>Http.post "https://api.example.com/users"</code></example>
- [`Flow.Http.put`](./m-flow-http-http-put.md):  Creates a PUT request. <example><code>Http.put "https://api.example.com/users/1"</code></example>
- [`Flow.Http.patch`](./m-flow-http-http-patch.md):  Creates a PATCH request. <example><code>Http.patch "https://api.example.com/users/1"</code></example>
- [`Flow.Http.delete`](./m-flow-http-http-delete.md):  Creates a DELETE request. <example><code>Http.delete "https://api.example.com/users/1"</code></example>
- [`Flow.Http.send`](./m-flow-http-http-send.md):  Sends a request and fails with <c>HttpError.Status</c> when the response is outside the expectation.
 <example><code>request |&gt; Http.send</code></example>
- [`Flow.Http.sendResult`](./m-flow-http-http-sendresult.md):  Sends a request and returns the transcript without interpreting the status expectation.
 <example><code>request |&gt; Http.sendResult</code></example>
- [`Flow.Http.text`](./m-flow-http-http-text.md):  Sends a request and returns the response body text.
 <example><code>Http.get url |&gt; Request.bearer token |&gt; Http.text</code></example>
- [`Flow.Http.bytes`](./m-flow-http-http-bytes.md):  Sends a request and returns the exact response body bytes.
 <example><code>Http.get url |&gt; Http.bytes</code></example>
- [`Flow.Http.json`](./m-flow-http-http-json.md):  Sends a request and decodes the JSON response body with the supplied decoder.
 <example><code>Http.get url |&gt; Request.acceptJson |&gt; Http.json (Json.deserializeResult codec)</code></example>
- [`Flow.Http.getString`](./m-flow-http-http-getstring.md):  Sends a GET request and returns the response body, mirroring <c>HttpClient.GetStringAsync</c>.
 <example><code>Http.getString "https://example.com"</code></example>
- [`Flow.Http.getBytes`](./m-flow-http-http-getbytes.md):  Sends a GET request and returns the body bytes, mirroring <c>HttpClient.GetByteArrayAsync</c>.
 <example><code>Http.getBytes "https://example.com/logo.png"</code></example>
- [`Flow.Http.getJson`](./m-flow-http-http-getjson.md):  Sends a GET request and decodes the JSON response.
 <example><code>Http.getJson (Json.deserializeResult codec) "https://api.example.com/users/1"</code></example>
- [`Flow.Http.postString`](./m-flow-http-http-poststring.md):  Sends a POST request with a text body, mirroring <c>HttpClient.PostAsync</c> with string content.
 <example><code>Http.postString "https://example.com/echo" "hello"</code></example>
- [`Flow.Http.postJson`](./m-flow-http-http-postjson.md):  Encodes a value as JSON, POSTs it, and decodes the JSON response.
 <example><code>Http.postJson (Json.serialize codec) (Json.deserializeResult codec) url user</code></example>
- [`Flow.Http.retryTransient`](./m-flow-http-http-retrytransient.md):  Retries a workflow on transient HTTP errors with exponential backoff.
 Permanent failures such as 404 or decode errors are never retried.
 <example><code>Http.getJson decode url |&gt; Http.retryTransient 4 (TimeSpan.FromMilliseconds 200.0)</code></example>

## Concise DSL

- [`Flow.Http.DSL.GET`](./m-flow-http-dsl-get.md):  Creates a GET request from an interpolated URL. Every hole is URL-encoded as one value.
 <example><code>GET $"https://api.example.com/users/{userId}"</code></example>
- [`Flow.Http.DSL.HEAD`](./m-flow-http-dsl-head.md):  Creates a HEAD request from an interpolated URL with encoded holes.
- [`Flow.Http.DSL.POST`](./m-flow-http-dsl-post.md):  Creates a POST request from an interpolated URL with encoded holes.
 <example><code>POST $"https://api.example.com/users" |&gt; jsonBody payload</code></example>
- [`Flow.Http.DSL.PUT`](./m-flow-http-dsl-put.md):  Creates a PUT request from an interpolated URL with encoded holes.
- [`Flow.Http.DSL.PATCH`](./m-flow-http-dsl-patch.md):  Creates a PATCH request from an interpolated URL with encoded holes.
- [`Flow.Http.DSL.DELETE`](./m-flow-http-dsl-delete.md):  Creates a DELETE request from an interpolated URL with encoded holes.
- [`Flow.Http.DSL.secret`](./m-flow-http-dsl-secret.md):  Marks an interpolated URL value for diagnostic redaction.
 <example><code>GET $"https://api.example.com/users?key={secret apiKey}"</code></example>
- [`Flow.Http.DSL.query`](./m-flow-http-dsl-query.md):  Appends a URL-encoded query parameter. <example><code>GET $"{root}/search" |&gt; query "q" term</code></example>
- [`Flow.Http.DSL.secretQuery`](./m-flow-http-dsl-secretquery.md):  Appends a redacted query parameter. <example><code>request |&gt; secretQuery "api_key" key</code></example>
- [`Flow.Http.DSL.header`](./m-flow-http-dsl-header.md):  Appends one request header. <example><code>request |&gt; header "Accept" "text/csv"</code></example>
- [`Flow.Http.DSL.bearer`](./m-flow-http-dsl-bearer.md):  Adds a redacted bearer-token Authorization header. <example><code>request |&gt; bearer token</code></example>
- [`Flow.Http.DSL.basicAuth`](./m-flow-http-dsl-basicauth.md):  Adds a redacted basic-auth Authorization header. <example><code>request |&gt; basicAuth user password</code></example>
- [`Flow.Http.DSL.timeout`](./m-flow-http-dsl-timeout.md):  Sets a per-request timeout. <example><code>request |&gt; timeout (TimeSpan.FromSeconds 5.0)</code></example>
- [`Flow.Http.DSL.jsonBody`](./m-flow-http-dsl-jsonbody.md):  Sends an already-serialized JSON body. <example><code>request |&gt; jsonBody """{"name":"Ada"}"""</code></example>
- [`Flow.Http.DSL.jsonBodyOf`](./m-flow-http-dsl-jsonbodyof.md):  Encodes and sends a JSON body. <example><code>request |&gt; jsonBodyOf (Json.serialize codec) user</code></example>
- [`Flow.Http.DSL.textBody`](./m-flow-http-dsl-textbody.md):  Sends a plain-text body. <example><code>request |&gt; textBody "hello"</code></example>
- [`Flow.Http.DSL.formBody`](./m-flow-http-dsl-formbody.md):  Sends URL-encoded form fields. <example><code>request |&gt; formBody [ "q", "axial" ]</code></example>
- [`Flow.Http.DSL.expect`](./m-flow-http-dsl-expect.md):  Replaces the statuses treated as success. <example><code>request |&gt; expect [ 200; 404 ]</code></example>
- [`Flow.Http.DSL.expectAny`](./m-flow-http-dsl-expectany.md):  Treats every status as success. <example><code>request |&gt; expectAny</code></example>
- [`Flow.Http.DSL.fetch`](./m-flow-http-dsl-fetch.md):  Sends the request and returns the full transcript, failing on unexpected statuses.
 <example><code>GET $"{root}/users" |&gt; fetch</code></example>
- [`Flow.Http.DSL.fetchText`](./m-flow-http-dsl-fetchtext.md):  Sends the request and returns the body text. <example><code>GET $"{root}/readme" |&gt; fetchText</code></example>
- [`Flow.Http.DSL.fetchBytes`](./m-flow-http-dsl-fetchbytes.md):  Sends the request and returns the body bytes. <example><code>GET $"{root}/logo.png" |&gt; fetchBytes</code></example>
- [`Flow.Http.DSL.fetchJson`](./m-flow-http-dsl-fetchjson.md):  Sends the request and decodes the JSON response.
 <example><code>GET $"{root}/users/{id}" |&gt; fetchJson (Json.deserializeResult codec)</code></example>
- [`Flow.Http.DSL.withRetries`](./m-flow-http-dsl-withretries.md):  Retries transient failures with exponential backoff.
 <example><code>GET $"{root}/users" |&gt; fetchJson decode |&gt; withRetries 4</code></example>

## Implementations

- [`Flow.Http.live`](./m-flow-http-http-live.md):  Creates a live HTTP service backed by <see cref="T:System.Net.Http.HttpClient" />.
 <example><code>Http.live (new HttpClient())</code></example>
- [`Flow.Http.layer`](./m-flow-http-http-layer.md):  Builds a live HTTP service as a layer.
 <example><code>Http.layer (new HttpClient())</code></example>
