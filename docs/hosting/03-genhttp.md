---
title: GenHttp
linkTitle: GenHttp
description: Schema-driven HTTP endpoints backed by Flow workflows on the GenHTTP server.
platform: dotnet
targetFramework: net8.0
---

# GenHttp

`Axial.Hosting.GenHttp` exposes Flow workflows as GenHTTP endpoints. Install it in the server project:

```bash
dotnet add package Axial.Hosting.GenHttp
```

`FlowEndpoint.flowEndpoint` builds a GenHTTP handler from a workflow, an application-environment projection, and an
error response mapper. `Request.json`, `Request.form`, and `Request.query` decode through a Reified schema; `Response` and
`SchemaResponse` write typed responses. `EndpointOptions` accepts an optional request-log sink; `None` uses the runtime
default.

The adapter keeps GenHTTP types at the boundary: the endpoint projection constructs the explicit environment the
workflow expects, so the workflow has no dependency on the server package.

## Request body size

`EndpointOptions.MaxRequestBodySize` sets the largest body one endpoint accepts, in bytes. Pass it through
`FlowEndpoint.flowEndpointWithOptions`, for example `{ EndpointOptions.Default with MaxRequestBodySize = Some 500_000_000L }`
for an upload endpoint. `None` is 30,000,000 bytes, the same as Kestrel's default on ASP.NET Core. `Request.json`,
`Request.form` and `Request.parseForm` stop reading past the limit and end the endpoint with a 413 problem response.

## Pages and forms

An endpoint can serve HTML as well as JSON. `Response.html` returns a page or the fragment an htmx request swaps in,
`Response.redirect` sends the browser on after a form post, and `Response.withHeader` adds a header such as
`HX-Push-Url` to any response.

`Request.form` (an `application/x-www-form-urlencoded` body) and `Request.query` end the endpoint with a 400 problem response when the input does not parse, which
suits an API client. A page instead shows the form again with each field's error and what the user typed:
`Request.parseForm` and `Request.parseQuery` return the parse result, so the endpoint renders either outcome. Field
names follow Reified's form convention: dotted names nest (`address.city`), numeric segments index lists (`tags.0`), and
an empty field is absent.

## Reference

The package API reference covers endpoint construction, request decoding, response helpers, and endpoint options.
