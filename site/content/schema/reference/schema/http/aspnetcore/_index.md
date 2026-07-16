---
title: "Schema HTTP ASP.NET Core"
weight: 500
type: docs
---

This page shows `Axial.Schema.Http.AspNetCore`. `Request` contributes schema-trusted values to an endpoint Flow, `EndpointFlow.run` embeds an HTTP-independent application workflow, `Response` constructs successful `IResult` values, and `flowEndpoint` lowers the completed Flow to the delegate accepted by ASP.NET Core routing. ASP.NET Core continues to own paths, verbs, middleware, authorization, filters, and endpoint metadata. The lower-level `SchemaRequest` and `SchemaResult` modules remain available when an endpoint needs the complete `ParsedInput` or direct host control.

## Endpoint model

- [`Schema.Http.AspNetCore.HttpEndpointEnv`](./t-schema-http-aspnetcore-httpendpointenv.md): The request-scoped environment supplied to an ASP.NET Core endpoint Flow.
- [`Schema.Http.AspNetCore.EndpointError`](./t-schema-http-aspnetcore-endpointerror.md): Distinguishes invalid request input from an expected application failure.

## Trusted request input

- [`Schema.Http.AspNetCore.Request.json`](./m-schema-http-aspnetcore-request-json.md): Reads and schema-parses a JSON request body; malformed JSON and schema diagnostics become invalid-request failures.
- [`Schema.Http.AspNetCore.Request.form`](./m-schema-http-aspnetcore-request-form.md): Reads and schema-parses a posted form.
- [`Schema.Http.AspNetCore.Request.query`](./m-schema-http-aspnetcore-request-query.md): Schema-parses the query string.
- [`Schema.Http.AspNetCore.Request.route`](./m-schema-http-aspnetcore-request-route.md): Schema-parses one named ASP.NET route value.

## Direct request input

- [`Schema.Http.AspNetCore.Request.raw`](./m-schema-http-aspnetcore-request-raw.md): Projects untrusted input directly from the native request without schema parsing.
- [`Schema.Http.AspNetCore.Request.native`](./m-schema-http-aspnetcore-request-native.md): Returns the native ASP.NET request for host-specific boundary handling.

## Application workflows

- [`Schema.Http.AspNetCore.EndpointFlow.run`](./m-schema-http-aspnetcore-endpointflow-run.md): Supplies <code>HttpEndpointEnv.App</code> to the application workflow and marks its typed failures as application errors.

## Successful responses

- [`Schema.Http.AspNetCore.Response.json`](./m-schema-http-aspnetcore-response-json.md): Streams a trusted value as JSON through a compiled codec.
- [`Schema.Http.AspNetCore.Response.text`](./m-schema-http-aspnetcore-response-text.md): Returns a plain-text response.
- [`Schema.Http.AspNetCore.Response.empty`](./m-schema-http-aspnetcore-response-empty.md): Returns an empty response with the supplied status code.
- [`Schema.Http.AspNetCore.Response.native`](./m-schema-http-aspnetcore-response-native.md): Returns a host-native ASP.NET result unchanged.

## Host lowering

- [`flowEndpoint`](./m-schema-http-aspnetcore-flowendpoint-flowendpoint.md): Lowers an endpoint Flow to the native ASP.NET Core handler expected by minimal-API routing.

## Lower-level request parsing

- [`Schema.Http.AspNetCore.SchemaRequest.json`](./m-schema-http-aspnetcore-schemarequest-json.md): Parses the JSON request body through the schema.
- [`Schema.Http.AspNetCore.SchemaRequest.form`](./m-schema-http-aspnetcore-schemarequest-form.md): Parses the posted form through the schema; dotted field names such as <code>address.street</code> nest.
- [`Schema.Http.AspNetCore.SchemaRequest.query`](./m-schema-http-aspnetcore-schemarequest-query.md): Parses the query string through the schema.

## Lower-level responses

- [`Schema.Http.AspNetCore.SchemaResult.problem`](./m-schema-http-aspnetcore-schemaresult-problem.md): A 400 <code>application/problem+json</code> response rendering the failed parse&#39;s diagnostics.
- [`Schema.Http.AspNetCore.SchemaResult.codec`](./m-schema-http-aspnetcore-schemaresult-codec.md): A JSON response streaming the trusted model through the compiled codec.
- [`Schema.Http.AspNetCore.SchemaResult.openApi`](./m-schema-http-aspnetcore-schemaresult-openapi.md): Serves a pre-assembled OpenAPI document (see <a href="https://learn.microsoft.com/dotnet/api/axial.schema.http.openapimodule.document">OpenApiModule.document</a>).
- [`Schema.Http.AspNetCore.SchemaResult.handleParsed`](./m-schema-http-aspnetcore-schemaresult-handleparsed.md): Runs the handler with the trusted model, or short-circuits to the problem-details response.
