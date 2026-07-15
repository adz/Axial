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
- `Axial.Schema.Http.AspNetCore` and `Axial.Schema.Http.GenHttp` adapt one host each. Their default API turns an
  ordinary `Flow` into that host's native handler; lower-level request parsing and response construction remain
  available when an endpoint does not use Flow. Routing, middleware, and app wiring stay in the host's own idiom.

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
open Microsoft.Extensions.DependencyInjection
open Axial.Flow
open Axial.Schema.Http.AspNetCore

type AppEnv =
    { SaveSignup: Signup -> Task<Signup> }

let createSignup signup : Flow<AppEnv, string, Signup> =
    flow {
        let! save = Flow.read _.SaveSignup
        return! save signup
    }

let signupEndpoint =
    flow {
        let! signup = Request.json Signup.schema
        let! created = EndpointFlow.run createSignup signup
        return Response.json 201 signupCodec created
    }

let endpoint =
    flowEndpoint
        (fun context -> context.RequestServices.GetRequiredService<AppEnv>())
        (fun error -> Results.BadRequest error)

app.MapPost("/signups", endpoint signupEndpoint)
```

`Request.json`, `Request.form`, and `Request.query` are Flow operations. They read the native request from the
endpoint environment and contribute only a trusted model; failed schema parsing becomes the standard problem-details
response. `EndpointFlow.run` supplies the application's explicit environment to a narrower application workflow and
marks its typed failures for the host error mapper. `Response.json` streams the successful value through a
[compiled codec](./json-codec.md).

`flowEndpoint` is the only lowering boundary. Its environment factory runs once per request, so it can return a
previously built environment or assemble one from ASP.NET's request-scoped service provider. Defects continue into
ASP.NET exception middleware, while interruption follows `RequestAborted`.

The lower-level `SchemaRequest` and `SchemaResult` modules remain available for endpoints that need the complete
`ParsedInput`, such as form redisplay.

Route text is untrusted too. Parse a scalar route value through its schema inside the same endpoint Flow:

```fsharp
let userEndpoint =
    flow {
        let! userId = Request.route "id" UserId.schema
        let! user = EndpointFlow.run findUser userId
        return Response.json 200 User.codec user
    }

app.MapGet("/users/{id}", endpoint userEndpoint)
```

Use `Request.raw projection` when deliberate direct mapping is enough, and `Request.native` or `Response.native` for
streaming, signatures, upgrades, or other host-specific behavior. Those names keep the loss of schema-established
trust visible at the call site.

The runnable version of this is `examples/Axial.Api` — one schema declaration driving parsing, problem details,
OpenAPI, a compiled response codec, and a redisplaying HTML form.

## GenHTTP

The same operations exist over GenHTTP's `IRequest`/`IResponse` for embedded servers:

```fsharp
open Axial.Flow
open GenHTTP.Modules.Functional
open Axial.Schema.Http.GenHttp

let createSignup signup : Flow<AppEnv, string, Signup> =
    flow {
        let! save = Flow.read _.SaveSignup
        return! save signup
    }

let signupEndpoint =
    flow {
        let! signup = Request.json Signup.schema
        let! created = EndpointFlow.run createSignup signup
        return Response.json ResponseStatus.Created signupCodec created
    }

let endpoint =
    flowEndpoint
        (fun _ -> appEnv)
        (Response.text ResponseStatus.BadRequest)

let handler =
    Inline.Create().Post("/signups", endpoint signupEndpoint)
```

The endpoint Flow and application workflow have the same structure on both hosts. Each adapter exposes its own
`Request`, `Response`, `HttpEndpointEnv`, and `flowEndpoint` types so the final value is the native handler expected by
that server. GenHTTP continues to own route registration.

## Form and query input

The core package also owns the host-neutral input rules, so every adapter produces identical `RawInput` for
identical wire data:

- `BoundaryInput.ofQuery` builds flat input from query pairs; repeated names become collections.
- `BoundaryInput.ofForm` nests dotted names (`address.street`), turns repeated names into collections, and turns
  sibling numeric segments (`tags.0`, `tags.1`) into ordered collections.

Form input is hostile, so `ofForm` drops contradictory pairs instead of raising. One consequence to know: a name
posted exactly once stays a scalar, because only the schema knows which fields are collections. A list field
submitted with a single selection should be posted as a repeated or indexed name.
