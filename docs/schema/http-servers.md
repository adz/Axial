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
  name/value input becomes `Data`, how parse diagnostics render as an error response, and how endpoint
  declarations assemble into an OpenAPI document.
- `Axial.Schema.Http.AspNetCore` and `Axial.Schema.Http.GenHttp` adapt one host each. Their default API turns an
  ordinary `Flow` into that host's native handler; lower-level request parsing and response construction remain
  available when an endpoint does not use Flow. Routing, middleware, and app wiring stay in the host's own idiom.

Because the contract lives in the core package, a service on Kestrel and a service embedding GenHTTP return the same
error bodies and publish the same OpenAPI fragments from the same schema declarations.

The host adapters intentionally have a larger dependency surface than the host-neutral package:

- `Axial.Schema.Http` depends on `Axial.Schema` and does not require Flow.
- `Axial.Schema.Http.AspNetCore` depends on `Axial.Schema.Http`, `Axial.Schema.Codec`, `Axial.Flow`, and ASP.NET Core.
- `Axial.Schema.Http.GenHttp` depends on `Axial.Schema.Http`, `Axial.Schema.Codec`, `Axial.Flow`, and GenHTTP.

Use the host-neutral package when parsing and rendering are enough. Install one host adapter when an HTTP handler should
run as a Flow.

Run `dotnet add package Axial.Schema.Http.AspNetCore` or
`dotnet add package Axial.Schema.Http.GenHttp`, depending on the server.

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

`ProblemDetails.ofParsed` builds that value from any failed `RetainedParseResult`; `ProblemDetails.ofDiagnosticsWith` does
the same for your own error type with your own renderer. `ProblemDetails.malformedJson` is the stable 400 value used
when a JSON body is not syntactically valid. Schema diagnostics and malformed JSON therefore share one media type and
response shape.

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

### Define the application workflow

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

The application workflow stays independent of ASP.NET Core:

```fsharp
createSignup : Signup -> Flow<AppEnv, string, Signup>
```

`EndpointFlow.run createSignup signup` adapts it to the endpoint Flow by projecting `HttpEndpointEnv.App` and wrapping
typed failures as `EndpointError.ApplicationError`. The endpoint Flow itself has this shape:

```fsharp
Flow<HttpEndpointEnv<AppEnv>, EndpointError<string>, IResult>
```

`HttpEndpointEnv.App` is the environment returned by the configured factory. `HttpEndpointEnv.Request` is the native
request used by `Request` operations; application workflows do not receive it.

### Build the application environment

`flowEndpoint` calls its environment factory once for every request. Resolve request-scoped ASP.NET services there:

```fsharp
let endpoint =
    flowEndpoint
        (fun context -> context.RequestServices.GetRequiredService<AppEnv>())
        ApiError.toResponse
```

Service-provider access is confined to this host boundary. If the environment was built at startup, capture it instead:

```fsharp
let endpoint =
    flowEndpoint
        (fun _ -> appEnv)
        ApiError.toResponse
```

The environment record is not copied deeply; it carries the same service references.

### Read request input

`Request.json`, `Request.form`, and `Request.query` are Flow operations. They read the native request from the
endpoint environment and contribute only a trusted model; failed schema parsing becomes the standard problem-details
response. `EndpointFlow.run` supplies the application's explicit environment to a narrower application workflow and
marks its typed failures for the host error mapper. `Response.json` streams the successful value through a
[compiled codec](./json-codec.md).

ASP.NET Core request operations are:

| Operation | Result |
| --- | --- |
| `Request.json schema` | Parses the JSON body; malformed JSON and schema failures become 400 problem details. |
| `Request.form schema` | Reads the posted form and parses its name/value input. |
| `Request.query schema` | Parses the complete query string. |
| `Request.route name schema` | Parses one scalar route value, or `Data.Null` when absent. |
| `Request.raw projection` | Projects an untrusted value directly from `HttpRequest` without establishing schema trust. |
| `Request.native` | Returns `HttpRequest` for deliberately host-specific handling. |

`Request.raw` does not catch exceptions from the projection. Such exceptions remain Flow defects and reach ASP.NET
exception middleware.

### Return successful responses

| Operation | Result |
| --- | --- |
| `Response.json status codec value` | Streams a trusted value as JSON through the compiled codec. |
| `Response.text status value` | Returns plain text with the supplied status. |
| `Response.empty status` | Returns an empty response with the supplied status. |
| `Response.native result` | Returns an existing ASP.NET `IResult` unchanged. |

The endpoint Flow may branch and return different response values. The typed application-error renderer is separate
from these successful response choices.

### Lower Flow outcomes

`flowEndpoint` is the only lowering boundary. Its environment factory runs once per request, so it can return a
previously built environment or assemble one from ASP.NET's request-scoped service provider. Defects continue into
ASP.NET exception middleware, while interruption follows `RequestAborted`.

The lowering rules are:

| Flow outcome | HTTP behavior |
| --- | --- |
| `Exit.Success result` | Returns the successful `IResult`. |
| `Cause.Fail (InvalidRequest problem)` | Returns the problem as `application/problem+json`. |
| `Cause.Fail (ApplicationError error)` | Calls the configured application-error renderer. |
| A traced single `Fail` | Preserves the same typed-failure behavior through the trace wrapper. |
| One `Die` defect | Rethrows the original exception into ASP.NET middleware. |
| Multiple defects | Throws an `AggregateException` containing every defect. |
| Interruption without defects | Throws `OperationCanceledException` using `RequestAborted`. |
| A compound typed-only cause | Throws `InvalidOperationException`; the adapter never chooses an arbitrary failure. |

A composite cause containing defects takes the defect path. This prevents parallel or sequential failures from being
silently collapsed into one application response.

The lower-level `SchemaRequest` and `SchemaResult` modules remain available for endpoints that need the complete
`RetainedParseResult`, such as form redisplay.

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

The same boundary model exists over GenHTTP's `IRequest`/`IResponse` for embedded servers:

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

GenHTTP provides `Request.json`, `Request.query`, `Request.raw`, and `Request.native`. It does not claim form or named
route-value support because the adapter does not have equivalent boundary implementations for those host surfaces.

GenHTTP responses are opaque `HttpResponse` plans because GenHTTP constructs `IResponse` from the current `IRequest`.
`Response.json`, `Response.text`, and `Response.empty` build those plans; `Response.native` accepts an
`IRequest -> IResponse` function for host-specific behavior. The application-error renderer supplied to
`flowEndpoint` returns the same `HttpResponse` plan type.

Outcome lowering matches ASP.NET Core for schema problems, application failures, defects, and compound causes.
GenHTTP's `IRequest` does not expose an equivalent of `HttpContext.RequestAborted` through this adapter, so
`flowEndpoint` starts the Flow without a request cancellation token.

## Complete API reference

- [Host-neutral schema HTTP boundary]({{< relref "/reference/schema/http/" >}})
- [ASP.NET Core adapter]({{< relref "/reference/schema/http/aspnetcore/" >}})
- [GenHTTP adapter]({{< relref "/reference/schema/http/genhttp/" >}})

The adapter reference pages include every `Request`, `Response`, `EndpointFlow`, and `flowEndpoint` member plus the
lower-level `SchemaRequest`, `SchemaResult`, and `SchemaResponse` surfaces.

## Form and query input

The core package also owns the host-neutral input rules, so every adapter produces identical `Data` for
identical wire data:

- `BoundaryInput.ofQuery` builds flat input from query pairs; repeated names become collections.
- `BoundaryInput.ofForm` nests dotted names (`address.street`), turns repeated names into collections, and turns
  sibling numeric segments (`tags.0`, `tags.1`) into ordered collections.

Form input is hostile, so `ofForm` drops contradictory pairs instead of raising. One consequence to know: a name
posted exactly once stays a scalar, because only the schema knows which fields are collections. A list field
submitted with a single selection should be posted as a repeated or indexed name.
