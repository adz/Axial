---
title: "Services Http"
weight: 40
type: docs
---

This page shows the HTTP client service package. Immutable `HttpRequest` values carry the method, encoded URL, headers, body, timeout, and status expectation; `Http.send` converts a request through the explicit `IHttp` capability and reports connection, timeout, status, and decode failures through `HttpError` with redacted request transcripts. The `DSL` module adds interpolated URL builders and terminal fetch verbs for concise call sites.

## Model

- [`Flow.HttpClient.Method`](./t-flow-httpclient-method.md):  Identifies one HTTP request method.
- [`Flow.HttpClient.RequestBody`](./t-flow-httpclient-requestbody.md):  Supplies the request payload and its media type.
- [`Flow.HttpClient.StatusExpectation`](./t-flow-httpclient-statusexpectation.md):  Decides which response status codes count as success for a request.
- [`Flow.HttpClient.HttpRequest`](./t-flow-httpclient-httprequest.md):  An immutable, fully described HTTP request.
- [`Flow.HttpClient.RequestPlan`](./t-flow-httpclient-requestplan.md):  A redacted, serializable description of a request that would be sent.
- [`Flow.HttpClient.HttpResponse`](./t-flow-httpclient-httpresponse.md):  The complete response transcript for one HTTP exchange.
- [`Flow.HttpClient.HttpError`](./t-flow-httpclient-httperror.md):  A recoverable HTTP transport, timeout, status, or decoding failure.

## Service

- [`Flow.HttpClient.IHttp`](./t-flow-httpclient-ihttp.md):  Sends fully described HTTP requests for a concrete host platform.

## Errors

- [`Flow.HttpClient.HttpError.describe`](./m-flow-httpclient-httperror-describe.md):  Formats an HTTP error with its redacted request context.
 <example><code>error |&gt; HttpError.describe</code></example>
- [`Flow.HttpClient.HttpError.tryResponse`](./m-flow-httpclient-httperror-tryresponse.md):  Returns the response transcript when the error carries one.
 <example><code>HttpError.tryResponse error</code></example>
- [`Flow.HttpClient.HttpError.isTransient`](./m-flow-httpclient-httperror-istransient.md):  Indicates whether retrying the same request could plausibly succeed.
 Connection failures, timeouts, and 408/429/5xx statuses are transient.
 <example><code>if HttpError.isTransient error then retry ()</code></example>
- [`Flow.HttpClient.HttpError.transientPolicy`](./m-flow-httpclient-httperror-transientpolicy.md):  Builds a retry policy with exponential backoff that retries only transient HTTP errors.
 <example><code>workflow |&gt; Flow.Runtime.retry (HttpError.transientPolicy 4 (TimeSpan.FromMilliseconds 200.0))</code></example>

## Request building

- [`Flow.HttpClient.Request.create`](./m-flow-httpclient-request-create.md):  Creates a request with the supplied method and already-formed URL.
 <example><code>Request.create Method.Get "https://api.example.com/users"</code></example>
- [`Flow.HttpClient.Request.query`](./m-flow-httpclient-request-query.md):  Appends one URL-encoded query parameter.
 <example><code>request |&gt; Request.query "page" 2</code></example>
- [`Flow.HttpClient.Request.secretQuery`](./m-flow-httpclient-request-secretquery.md):  Appends a query parameter whose value is replaced with <c>***</c> in plans and error transcripts.
 <example><code>request |&gt; Request.secretQuery "api_key" key</code></example>
- [`Flow.HttpClient.Request.header`](./m-flow-httpclient-request-header.md):  Appends one request header.
 <example><code>request |&gt; Request.header "Accept" "application/json"</code></example>
- [`Flow.HttpClient.Request.secretHeader`](./m-flow-httpclient-request-secretheader.md):  Appends a header whose value is replaced with <c>***</c> in plans and error transcripts.
 <example><code>request |&gt; Request.secretHeader "X-Api-Key" key</code></example>
- [`Flow.HttpClient.Request.bearer`](./m-flow-httpclient-request-bearer.md):  Adds a bearer-token Authorization header. The token is always redacted in diagnostics.
 <example><code>request |&gt; Request.bearer token</code></example>
- [`Flow.HttpClient.Request.basicAuth`](./m-flow-httpclient-request-basicauth.md):  Adds a basic-auth Authorization header. The credentials are always redacted in diagnostics.
 <example><code>request |&gt; Request.basicAuth user password</code></example>
- [`Flow.HttpClient.Request.accept`](./m-flow-httpclient-request-accept.md):  Sets the Accept header. <example><code>request |&gt; Request.accept "application/json"</code></example>
- [`Flow.HttpClient.Request.acceptJson`](./m-flow-httpclient-request-acceptjson.md):  Sets the Accept header to <c>application/json</c>. <example><code>request |&gt; Request.acceptJson</code></example>
- [`Flow.HttpClient.Request.userAgent`](./m-flow-httpclient-request-useragent.md):  Sets the User-Agent header. <example><code>request |&gt; Request.userAgent "axial-app/1.0"</code></example>
- [`Flow.HttpClient.Request.timeout`](./m-flow-httpclient-request-timeout.md):  Sets a per-request timeout enforced by the live service.
 <example><code>request |&gt; Request.timeout (TimeSpan.FromSeconds 5.0)</code></example>
- [`Flow.HttpClient.Request.textBody`](./m-flow-httpclient-request-textbody.md):  Sends a plain-text body. <example><code>request |&gt; Request.textBody "hello"</code></example>
- [`Flow.HttpClient.Request.jsonBody`](./m-flow-httpclient-request-jsonbody.md):  Sends an already-serialized JSON body with the <c>application/json</c> content type.
 <example><code>request |&gt; Request.jsonBody """{"name":"Ada"}"""</code></example>
- [`Flow.HttpClient.Request.jsonBodyWith`](./m-flow-httpclient-request-jsonbodywith.md):  Encodes a value with the supplied serializer and sends it as JSON.
 <example><code>request |&gt; Request.jsonBodyWith (Json.serialize codec) user</code></example>
- [`Flow.HttpClient.Request.bytesBody`](./m-flow-httpclient-request-bytesbody.md):  Sends raw bytes with an explicit content type.
 <example><code>request |&gt; Request.bytesBody "application/octet-stream" payload</code></example>
- [`Flow.HttpClient.Request.formBody`](./m-flow-httpclient-request-formbody.md):  Sends URL-encoded form fields. <example><code>request |&gt; Request.formBody [ "q", "axial" ]</code></example>
- [`Flow.HttpClient.Request.expect`](./m-flow-httpclient-request-expect.md):  Replaces the statuses treated as success.
 <example><code>request |&gt; Request.expect [ 200; 404 ]</code></example>
- [`Flow.HttpClient.Request.expectAny`](./m-flow-httpclient-request-expectany.md):  Treats every status as success so the caller can branch on the code explicitly.
 <example><code>request |&gt; Request.expectAny</code></example>
- [`Flow.HttpClient.Request.render`](./m-flow-httpclient-request-render.md):  Renders the redacted request line used in error transcripts.
 <example><code>Request.render request</code></example>
- [`Flow.HttpClient.Request.plan`](./m-flow-httpclient-request-plan.md):  Returns a redacted request plan without sending anything.
 <example><code>Request.plan request</code></example>

## Responses

- [`Flow.HttpClient.Response.text`](./m-flow-httpclient-response-text.md):  Returns the response body text. <example><code>response |&gt; Response.text</code></example>
- [`Flow.HttpClient.Response.bytes`](./m-flow-httpclient-response-bytes.md):  Returns the exact response body bytes. <example><code>response |&gt; Response.bytes</code></example>
- [`Flow.HttpClient.Response.statusCode`](./m-flow-httpclient-response-statuscode.md):  Returns the response status code. <example><code>response |&gt; Response.statusCode</code></example>
- [`Flow.HttpClient.Response.tryHeader`](./m-flow-httpclient-response-tryheader.md):  Finds the first header with the given case-insensitive name.
 <example><code>response |&gt; Response.tryHeader "ETag"</code></example>
- [`Flow.HttpClient.Response.json`](./m-flow-httpclient-response-json.md):  Decodes the response body with the supplied decoder, mapping failure to <c>HttpError.DecodeFailed</c>.
 <example><code>response |&gt; Response.json (Json.deserializeResult codec)</code></example>
- [`Flow.HttpClient.Response.create`](./m-flow-httpclient-response-create.md):  Creates a synthetic response transcript at an explicit start time, primarily for test fakes.
 <example><code>Response.create startedAt 200 """{"ok":true}"""</code></example>

## Execution

- [`Flow.HttpClient.Http.get`](./m-flow-httpclient-http-get.md):  Creates a GET request. <example><code>Http.get "https://api.example.com/users"</code></example>
- [`Flow.HttpClient.Http.head`](./m-flow-httpclient-http-head.md):  Creates a HEAD request. <example><code>Http.head "https://api.example.com/users"</code></example>
- [`Flow.HttpClient.Http.post`](./m-flow-httpclient-http-post.md):  Creates a POST request. <example><code>Http.post "https://api.example.com/users"</code></example>
- [`Flow.HttpClient.Http.put`](./m-flow-httpclient-http-put.md):  Creates a PUT request. <example><code>Http.put "https://api.example.com/users/1"</code></example>
- [`Flow.HttpClient.Http.patch`](./m-flow-httpclient-http-patch.md):  Creates a PATCH request. <example><code>Http.patch "https://api.example.com/users/1"</code></example>
- [`Flow.HttpClient.Http.delete`](./m-flow-httpclient-http-delete.md):  Creates a DELETE request. <example><code>Http.delete "https://api.example.com/users/1"</code></example>
- [`Flow.HttpClient.Http.send`](./m-flow-httpclient-http-send.md):  Sends a request and fails with <c>HttpError.Status</c> when the response is outside the expectation.
 <example><code>request |&gt; Http.send</code></example>
- [`Flow.HttpClient.Http.sendResult`](./m-flow-httpclient-http-sendresult.md):  Sends a request and returns the transcript without interpreting the status expectation.
 <example><code>request |&gt; Http.sendResult</code></example>
- [`Flow.HttpClient.Http.text`](./m-flow-httpclient-http-text.md):  Sends a request and returns the response body text.
 <example><code>Http.get url |&gt; Request.bearer token |&gt; Http.text</code></example>
- [`Flow.HttpClient.Http.bytes`](./m-flow-httpclient-http-bytes.md):  Sends a request and returns the exact response body bytes.
 <example><code>Http.get url |&gt; Http.bytes</code></example>
- [`Flow.HttpClient.Http.json`](./m-flow-httpclient-http-json.md):  Sends a request and decodes the JSON response body with the supplied decoder.
 <example><code>Http.get url |&gt; Request.acceptJson |&gt; Http.json (Json.deserializeResult codec)</code></example>
- [`Flow.HttpClient.Http.getString`](./m-flow-httpclient-http-getstring.md):  Sends a GET request and returns the response body, mirroring <c>HttpClient.GetStringAsync</c>.
 <example><code>Http.getString "https://example.com"</code></example>
- [`Flow.HttpClient.Http.getBytes`](./m-flow-httpclient-http-getbytes.md):  Sends a GET request and returns the body bytes, mirroring <c>HttpClient.GetByteArrayAsync</c>.
 <example><code>Http.getBytes "https://example.com/logo.png"</code></example>
- [`Flow.HttpClient.Http.getJson`](./m-flow-httpclient-http-getjson.md):  Sends a GET request and decodes the JSON response.
 <example><code>Http.getJson (Json.deserializeResult codec) "https://api.example.com/users/1"</code></example>
- [`Flow.HttpClient.Http.postString`](./m-flow-httpclient-http-poststring.md):  Sends a POST request with a text body, mirroring <c>HttpClient.PostAsync</c> with string content.
 <example><code>Http.postString "https://example.com/echo" "hello"</code></example>
- [`Flow.HttpClient.Http.postJson`](./m-flow-httpclient-http-postjson.md):  Encodes a value as JSON, POSTs it, and decodes the JSON response.
 <example><code>Http.postJson (Json.serialize codec) (Json.deserializeResult codec) url user</code></example>
- [`Flow.HttpClient.Http.retryTransient`](./m-flow-httpclient-http-retrytransient.md):  Retries a workflow on transient HTTP errors with exponential backoff.
 Permanent failures such as 404 or decode errors are never retried.
 <example><code>Http.getJson decode url |&gt; Http.retryTransient 4 (TimeSpan.FromMilliseconds 200.0)</code></example>

## Concise DSL

- [`Flow.HttpClient.DSL.GET`](./m-flow-httpclient-dsl-get.md):  Creates a GET request from an interpolated URL. Every hole is URL-encoded as one value.
 <example><code>GET $"https://api.example.com/users/{userId}"</code></example>
- [`Flow.HttpClient.DSL.HEAD`](./m-flow-httpclient-dsl-head.md):  Creates a HEAD request from an interpolated URL with encoded holes.
- [`Flow.HttpClient.DSL.POST`](./m-flow-httpclient-dsl-post.md):  Creates a POST request from an interpolated URL with encoded holes.
 <example><code>POST $"https://api.example.com/users" |&gt; jsonBody payload</code></example>
- [`Flow.HttpClient.DSL.PUT`](./m-flow-httpclient-dsl-put.md):  Creates a PUT request from an interpolated URL with encoded holes.
- [`Flow.HttpClient.DSL.PATCH`](./m-flow-httpclient-dsl-patch.md):  Creates a PATCH request from an interpolated URL with encoded holes.
- [`Flow.HttpClient.DSL.DELETE`](./m-flow-httpclient-dsl-delete.md):  Creates a DELETE request from an interpolated URL with encoded holes.
- [`Flow.HttpClient.DSL.secret`](./m-flow-httpclient-dsl-secret.md):  Marks an interpolated URL value for diagnostic redaction.
 <example><code>GET $"https://api.example.com/users?key={secret apiKey}"</code></example>
- [`Flow.HttpClient.DSL.query`](./m-flow-httpclient-dsl-query.md):  Appends a URL-encoded query parameter. <example><code>GET $"{root}/search" |&gt; query "q" term</code></example>
- [`Flow.HttpClient.DSL.secretQuery`](./m-flow-httpclient-dsl-secretquery.md):  Appends a redacted query parameter. <example><code>request |&gt; secretQuery "api_key" key</code></example>
- [`Flow.HttpClient.DSL.header`](./m-flow-httpclient-dsl-header.md):  Appends one request header. <example><code>request |&gt; header "Accept" "text/csv"</code></example>
- [`Flow.HttpClient.DSL.bearer`](./m-flow-httpclient-dsl-bearer.md):  Adds a redacted bearer-token Authorization header. <example><code>request |&gt; bearer token</code></example>
- [`Flow.HttpClient.DSL.basicAuth`](./m-flow-httpclient-dsl-basicauth.md):  Adds a redacted basic-auth Authorization header. <example><code>request |&gt; basicAuth user password</code></example>
- [`Flow.HttpClient.DSL.timeout`](./m-flow-httpclient-dsl-timeout.md):  Sets a per-request timeout. <example><code>request |&gt; timeout (TimeSpan.FromSeconds 5.0)</code></example>
- [`Flow.HttpClient.DSL.jsonBody`](./m-flow-httpclient-dsl-jsonbody.md):  Sends an already-serialized JSON body. <example><code>request |&gt; jsonBody """{"name":"Ada"}"""</code></example>
- [`Flow.HttpClient.DSL.jsonBodyOf`](./m-flow-httpclient-dsl-jsonbodyof.md):  Encodes and sends a JSON body. <example><code>request |&gt; jsonBodyOf (Json.serialize codec) user</code></example>
- [`Flow.HttpClient.DSL.textBody`](./m-flow-httpclient-dsl-textbody.md):  Sends a plain-text body. <example><code>request |&gt; textBody "hello"</code></example>
- [`Flow.HttpClient.DSL.formBody`](./m-flow-httpclient-dsl-formbody.md):  Sends URL-encoded form fields. <example><code>request |&gt; formBody [ "q", "axial" ]</code></example>
- [`Flow.HttpClient.DSL.expect`](./m-flow-httpclient-dsl-expect.md):  Replaces the statuses treated as success. <example><code>request |&gt; expect [ 200; 404 ]</code></example>
- [`Flow.HttpClient.DSL.expectAny`](./m-flow-httpclient-dsl-expectany.md):  Treats every status as success. <example><code>request |&gt; expectAny</code></example>
- [`Flow.HttpClient.DSL.fetch`](./m-flow-httpclient-dsl-fetch.md):  Sends the request and returns the full transcript, failing on unexpected statuses.
 <example><code>GET $"{root}/users" |&gt; fetch</code></example>
- [`Flow.HttpClient.DSL.fetchText`](./m-flow-httpclient-dsl-fetchtext.md):  Sends the request and returns the body text. <example><code>GET $"{root}/readme" |&gt; fetchText</code></example>
- [`Flow.HttpClient.DSL.fetchBytes`](./m-flow-httpclient-dsl-fetchbytes.md):  Sends the request and returns the body bytes. <example><code>GET $"{root}/logo.png" |&gt; fetchBytes</code></example>
- [`Flow.HttpClient.DSL.fetchJson`](./m-flow-httpclient-dsl-fetchjson.md):  Sends the request and decodes the JSON response.
 <example><code>GET $"{root}/users/{id}" |&gt; fetchJson (Json.deserializeResult codec)</code></example>
- [`Flow.HttpClient.DSL.withRetries`](./m-flow-httpclient-dsl-withretries.md):  Retries transient failures with exponential backoff.
 <example><code>GET $"{root}/users" |&gt; fetchJson decode |&gt; withRetries 4</code></example>

## Implementations

- [`Flow.HttpClient.Http.live`](./m-flow-httpclient-http-live.md):  Creates a live HTTP service backed by an explicit clock and <see cref="T:System.Net.Http.HttpClient" />.
 <example><code>Http.live Clock.live (new HttpClient())</code></example>
- [`Flow.HttpClient.Http.layer`](./m-flow-httpclient-http-layer.md):  Builds a live HTTP service from an explicit clock as a layer.
 <example><code>Http.layer Clock.live (new HttpClient())</code></example>
