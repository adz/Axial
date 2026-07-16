---
title: "Schema HTTP GenHTTP"
weight: 500
---

This page shows `Axial.Schema.Http.GenHttp`. `Request` contributes schema-trusted values to an endpoint Flow, `EndpointFlow.run` embeds an HTTP-independent application workflow, `Response` constructs request-relative response plans, and `flowEndpoint` lowers the completed Flow to the delegate accepted by GenHTTP routing. GenHTTP continues to own paths, verbs, and handler composition. The lower-level `SchemaRequest` and `SchemaResponse` modules remain available when an endpoint needs the complete `ParsedInput` or direct host control.

## Endpoint model

- [`Schema.Http.GenHttp.HttpEndpointEnv`](./t-schema-http-genhttp-httpendpointenv.md): The request-scoped environment supplied to a GenHTTP endpoint Flow.
- [`Schema.Http.GenHttp.EndpointError`](./t-schema-http-genhttp-endpointerror.md): Distinguishes invalid request input from an expected application failure.
- [`Schema.Http.GenHttp.HttpResponse`](./t-schema-http-genhttp-httpresponse.md): A response plan that GenHTTP executes against the current native request.

## Trusted request input

- [`Schema.Http.GenHttp.Request.json`](./m-schema-http-genhttp-request-json.md): Reads and schema-parses a JSON request body; malformed JSON and schema diagnostics become invalid-request failures.
- [`Schema.Http.GenHttp.Request.query`](./m-schema-http-genhttp-request-query.md): Schema-parses the query string.

## Direct request input

- [`Schema.Http.GenHttp.Request.raw`](./m-schema-http-genhttp-request-raw.md): Projects untrusted input directly from the native request without schema parsing.
- [`Schema.Http.GenHttp.Request.native`](./m-schema-http-genhttp-request-native.md): Returns the native GenHTTP request for host-specific boundary handling.

## Application workflows

- [`Schema.Http.GenHttp.EndpointFlow.run`](./m-schema-http-genhttp-endpointflow-run.md): Supplies <code>HttpEndpointEnv.App</code> to the application workflow and marks its typed failures as application errors.

## Successful responses

- [`Schema.Http.GenHttp.Response.json`](./m-schema-http-genhttp-response-json.md): Serializes a trusted value as JSON through a compiled codec.
- [`Schema.Http.GenHttp.Response.text`](./m-schema-http-genhttp-response-text.md): Returns a plain-text response.
- [`Schema.Http.GenHttp.Response.empty`](./m-schema-http-genhttp-response-empty.md): Returns an empty response with the supplied status.
- [`Schema.Http.GenHttp.Response.native`](./m-schema-http-genhttp-response-native.md): Builds a host-native response plan from the current GenHTTP request.

## Host lowering

- [`flowEndpoint`](./m-schema-http-genhttp-flowendpoint-flowendpoint.md): Lowers an endpoint Flow to the native handler expected by GenHTTP routing.

## Lower-level request parsing

- [`Schema.Http.GenHttp.SchemaRequest.json`](./m-schema-http-genhttp-schemarequest-json.md): Parses the JSON request body through the schema; a missing body parses as missing input.
- [`Schema.Http.GenHttp.SchemaRequest.query`](./m-schema-http-genhttp-schemarequest-query.md): Parses the query string through the schema.

## Lower-level responses

- [`Schema.Http.GenHttp.SchemaResponse.problem`](./m-schema-http-genhttp-schemaresponse-problem.md): A 400 <code>application/problem+json</code> response rendering the failed parse&#39;s diagnostics.
- [`Schema.Http.GenHttp.SchemaResponse.codec`](./m-schema-http-genhttp-schemaresponse-codec.md): A JSON response rendering the trusted model through the compiled codec.
- [`Schema.Http.GenHttp.SchemaResponse.openApi`](./m-schema-http-genhttp-schemaresponse-openapi.md): Serves a pre-assembled OpenAPI document (see <a href="https://learn.microsoft.com/dotnet/api/axial.schema.http.openapimodule.document">OpenApiModule.document</a>).
- [`Schema.Http.GenHttp.SchemaResponse.handleParsed`](./m-schema-http-genhttp-schemaresponse-handleparsed.md): Runs the handler with the trusted model, or short-circuits to the problem-details response.
