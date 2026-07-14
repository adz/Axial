---
weight: 60
title: HTTP Servers
type: docs
description: Schema-driven endpoints with problem-details errors and generated OpenAPI, on ASP.NET Core or GenHTTP.
---

# HTTP Servers

An HTTP endpoint is a trust boundary: the body, form, or query string arrives untrusted, and the handler wants a
typed model. `Axial.Schema.Http` turns one schema declaration into everything that boundary needs — parsing, an
error contract, and the published API document — and stays out of routing entirely.

The package split matters:

- `Axial.Schema.Http` is host-neutral. It depends only on `Axial.Schema` and defines the boundary contract: how
  name/value input becomes `RawInput`, how parse diagnostics render as an error response, and how endpoint
  declarations assemble into an OpenAPI document.
- `Axial.Schema.Http.AspNetCore` and `Axial.Schema.Http.GenHttp` adapt one host each. They parse that host's request
  into `ParsedInput` and build that host's response type. Routing, middleware, and app wiring stay in the host's own
  idiom — there is deliberately no cross-host application abstraction.

Because the contract lives in the core package, a service on Kestrel and a service embedding GenHTTP return the same
error bodies and publish the same OpenAPI fragments from the same schema declarations.

## The error contract

A failed parse becomes an RFC 9457 problem-details body served as `application/problem+json`. Each diagnostic keeps
its path as an RFC 6901 JSON pointer, so clients can attach errors to fields mechanically:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc9457",
  "title": "The request input could not be parsed.",
  "status": 400,
  "errors": [
    { "pointer": "/address/city", "message": "This value is required." },
    { "pointer": "/tags", "message": "Count must be at most 5; got 6." }
  ]
}
```

`ProblemDetails.ofParsed` builds that value from any failed `ParsedInput`; `ProblemDetails.ofDiagnosticsWith` does
the same for your own error type with your own renderer.

## Declaring endpoints for OpenAPI

`EndpointSpec` describes the boundary contract of one endpoint — method, path, request schema, responses — and
`OpenApi.document` assembles the specs into an OpenAPI 3.1 document. Request and response schemas are embedded from
`JsonSchema.generate` output, so the published contract cannot drift from what the parser accepts:

```fsharp
open Axial.Schema.Http

let openApiDocument =
    OpenApi.document
        (OpenApi.info "Signup API" "1.0.0")
        [ Endpoint.post "/signups"
          |> Endpoint.summary "Create a signup"
          |> Endpoint.accepts Signup.schema
          |> Endpoint.returnsJson 201 "The trusted signup that was parsed." Signup.schema
          |> Endpoint.returnsProblemDetails ]
```

`Endpoint.returnsProblemDetails` adds the standard 400 response with the problem-details JSON Schema, so the error
contract is part of the published document too.

## ASP.NET Core

```fsharp
open Axial.Schema.Http.AspNetCore

app.MapPost(
    "/signups",
    Func<HttpRequest, Task<IResult>>(fun request ->
        task {
            let! parsed = SchemaRequest.json Signup.schema request

            return!
                parsed
                |> SchemaResult.handleParsed (fun signup ->
                    Task.FromResult(SchemaResult.codec signupCodec 201 signup))
        })
)
```

`SchemaRequest.json`, `SchemaRequest.form`, and `SchemaRequest.query` parse the request into `ParsedInput`.
`SchemaResult.handleParsed` runs the handler with the trusted model or short-circuits to the problem-details
response; `SchemaResult.codec` streams the response through a [compiled codec](./json-codec.md) without an
intermediate string; `SchemaResult.openApi` serves the assembled document.

The runnable version of this is `examples/Axial.Api` — one schema declaration driving parsing, problem details,
OpenAPI, a compiled response codec, and a redisplaying HTML form.

## GenHTTP

The same operations exist over GenHTTP's `IRequest`/`IResponse` for embedded servers:

```fsharp
open GenHTTP.Modules.Functional
open Axial.Schema.Http.GenHttp

let handler =
    Inline
        .Create()
        .Post(
            "/signups",
            Func<IRequest, ValueTask<IResponse>>(fun request ->
                ValueTask<IResponse>(
                    task {
                        let! parsed = SchemaRequest.json Signup.schema request

                        return!
                            (parsed
                             |> SchemaResponse.handleParsed request (fun signup ->
                                 ValueTask<IResponse>(
                                     SchemaResponse.codec signupCodec ResponseStatus.Created request signup
                                 )))
                                .AsTask()
                    }
                ))
        )
        .Get("/openapi.json", Func<IRequest, IResponse>(fun request -> SchemaResponse.openApi request openApiDocument))
```

## Form and query input

The core package also owns the host-neutral input rules, so every adapter produces identical `RawInput` for
identical wire data:

- `BoundaryInput.ofQuery` builds flat input from query pairs; repeated names become collections.
- `BoundaryInput.ofForm` nests dotted names (`address.street`), turns repeated names into collections, and turns
  sibling numeric segments (`tags.0`, `tags.1`) into ordered collections.

Form input is hostile, so `ofForm` drops contradictory pairs instead of raising. One consequence to know: a name
posted exactly once stays a scalar, because only the schema knows which fields are collections. A list field
submitted with a single selection should be posted as a repeated or indexed name.
