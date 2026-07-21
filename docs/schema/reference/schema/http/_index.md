---
title: "Schema HTTP Boundary"
weight: 500
---

This page shows the host-neutral server boundary in `Axial.Schema.Http`: `BoundaryInput` builds structured data from the name/value surfaces HTTP servers hand over, `ProblemDetails` renders failed parses as RFC 9457 bodies with RFC 6901 JSON pointers, and `EndpointSpec` values assemble into OpenAPI 3.1 documents whose schemas are embedded from `JsonSchema.generate` output. Host-specific Flow lowering is documented under [ASP.NET Core](./aspnetcore/) and [GenHTTP](./genhttp/); see the [HTTP servers guide](/schema/http-servers/) for complete usage.

## Boundary input

- [`Schema.Http.BoundaryInput.ofQuery`](./m-schema-http-boundaryinput-ofquery.md): Builds object-shaped structured data from query-string pairs, grouping repeated names into collections.
- [`Schema.Http.BoundaryInput.ofForm`](./m-schema-http-boundaryinput-ofform.md): Builds structured data from form pairs, where dotted names such as <code>address.street</code> nest.

## Problem details

- [`Schema.Http.ProblemDetails`](./t-schema-http-problemdetails.md): An RFC 9457 problem-details value carrying path-aware parse errors.
- [`Schema.Http.ProblemError`](./t-schema-http-problemerror.md): One boundary error: a JSON pointer into the request body plus a rendered message.
- [`Schema.Http.ProblemDetails.malformedJson`](./p-schema-http-problemdetails-malformedjson.md): Builds a 400 problem-details value for a syntactically invalid JSON request body.
- [`Schema.Http.ProblemDetails.ofParsed`](./m-schema-http-problemdetails-ofparsed.md): Builds a 400 problem-details value from a failed parse, or <code>None</code> when parsing succeeded.
- [`Schema.Http.ProblemDetails.ofDiagnostics`](./m-schema-http-problemdetails-ofdiagnostics.md): Builds a 400 problem-details value from failed schema parse diagnostics.
- [`Schema.Http.ProblemDetails.ofDiagnosticsWith`](./m-schema-http-problemdetails-ofdiagnosticswith.md): Builds a 400 problem-details value from parse diagnostics, rendering each error with <span class="fsdocs-param-name">render</span>.
- [`Schema.Http.ProblemDetails.toJson`](./m-schema-http-problemdetails-tojson.md): Renders the problem-details JSON body as a string.
- [`Schema.Http.ProblemDetails.writeTo`](./m-schema-http-problemdetails-writeto.md): Writes the problem-details JSON body to a stream.
- [`Schema.Http.JsonPointer.ofPath`](./m-schema-http-jsonpointer-ofpath.md): Renders a diagnostics path as a JSON pointer. The empty path renders as <code>&quot;&quot;</code> (the whole document).

## Endpoint specs

- [`Schema.Http.EndpointSpec`](./t-schema-http-endpointspec.md): A host-neutral description of one schema-driven endpoint, used to assemble OpenAPI documents.
- [`Schema.Http.ResponseSpec`](./t-schema-http-responsespec.md): One documented response of an endpoint.
- [`Schema.Http.Endpoint.get`](./m-schema-http-endpoint-get.md): Starts a GET endpoint spec at the supplied path.
- [`Schema.Http.Endpoint.post`](./m-schema-http-endpoint-post.md): Starts a POST endpoint spec at the supplied path.
- [`Schema.Http.Endpoint.put`](./m-schema-http-endpoint-put.md): Starts a PUT endpoint spec at the supplied path.
- [`Schema.Http.Endpoint.patch`](./m-schema-http-endpoint-patch.md): Starts a PATCH endpoint spec at the supplied path.
- [`Schema.Http.Endpoint.delete`](./m-schema-http-endpoint-delete.md): Starts a DELETE endpoint spec at the supplied path.
- [`Schema.Http.Endpoint.summary`](./m-schema-http-endpoint-summary.md): Sets the operation summary shown in generated documents.
- [`Schema.Http.Endpoint.operationId`](./m-schema-http-endpoint-operationid.md): Sets the OpenAPI operation id.
- [`Schema.Http.Endpoint.tag`](./m-schema-http-endpoint-tag.md): Appends an OpenAPI tag used to group operations.
- [`Schema.Http.Endpoint.accepts`](./m-schema-http-endpoint-accepts.md): Declares the request body: JSON described by the schema's generated JSON Schema.
- [`Schema.Http.Endpoint.returnsJson`](./m-schema-http-endpoint-returnsjson.md): Adds a JSON response whose body is described by the schema's generated JSON Schema.
- [`Schema.Http.Endpoint.returns`](./m-schema-http-endpoint-returns.md): Adds a body-less response.
- [`Schema.Http.Endpoint.returnsProblemDetails`](./m-schema-http-endpoint-returnsproblemdetails.md): Adds the standard 400 problem-details response every schema-parsing endpoint produces.

## OpenAPI assembly

- [`Schema.Http.OpenApiInfo`](./t-schema-http-openapiinfo.md): Document-level OpenAPI metadata.
- [`Schema.Http.OpenApi.info`](./m-schema-http-openapi-info.md): Builds document metadata with no description.
- [`Schema.Http.OpenApi.document`](./m-schema-http-openapi-document.md): Renders an OpenAPI 3.1 JSON document covering the supplied endpoints.
- [`Schema.Http.OpenApi.writeTo`](./m-schema-http-openapi-writeto.md): Writes an OpenAPI 3.1 JSON document covering the supplied endpoints to a stream.
