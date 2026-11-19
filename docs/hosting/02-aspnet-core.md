---
title: ASP.NET Core
linkTitle: ASP.NET Core
description: Schema-driven HTTP endpoints backed by Flow workflows.
platform: dotnet
targetFramework: net8.0
---

# ASP.NET Core

`Axial.Hosting.AspNetCore` connects Flow workflows to ASP.NET Core endpoints. Install it alongside the ASP.NET Core
application:

```bash
dotnet add package Axial.Hosting.AspNetCore
```

`FlowEndpoint.flowEndpoint` builds an endpoint handler from a workflow, an application-environment projection, and an
error response mapper. `Request.json`, `Request.form`, `Request.query`, and `Request.route` decode the request through a Reified schema;
`Response` and `SchemaResult` write typed responses. `EndpointOptions` accepts the request-log sink; `None` leaves the
runtime default.

The host-facing shape keeps the application environment at the edge: the endpoint projection constructs the same
explicit record or layer that the workflow already expects, and the workflow remains independent of ASP.NET Core types.

## Request body size

`EndpointOptions.MaxRequestBodySize` sets the largest body one endpoint accepts, in bytes. Pass it through
`FlowEndpoint.flowEndpointWithOptions`, for example `{ EndpointOptions.Default with MaxRequestBodySize = Some 500_000_000L }`
for an upload endpoint. It sets the request's `IHttpMaxRequestBodySizeFeature`, as `RequestSizeLimitAttribute` does.
`None` keeps the server's limit, which is 30,000,000 bytes unless Kestrel is configured otherwise. A larger body ends
`Request.json`, `Request.form` and `Request.parseForm` with a 413 problem response. `Request.form` also answers 413 when
a form passes ASP.NET Core's `FormOptions` limits, such as a single value over 4 MiB.

## Pages and forms

An endpoint can serve HTML as well as JSON. `Response.html` returns a page or the fragment an htmx request swaps in,
`Response.redirect` sends the browser on after a form post, and `Response.withHeader` adds a header such as
`HX-Push-Url` to any response.

`Request.form` and `Request.query` end the endpoint with a 400 problem response when the input does not parse, which
suits an API client. A page instead shows the form again with each field's error and what the user typed:
`Request.parseForm` and `Request.parseQuery` return the parse result, so the endpoint renders either outcome. Field
names follow Reified's form convention: dotted names nest (`address.city`), numeric segments index lists (`tags.0`), and
an empty field is absent.

## Reference

The package API reference covers endpoint construction, request decoding, response helpers, and endpoint options. See
[running as a service](/hosting/running-as-a-service.html) for the process-level lifecycle around the web host.
