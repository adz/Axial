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
error response mapper. `Request.json` and `Request.query` decode through a Reified schema; `Response` and
`SchemaResult` write typed responses. `EndpointOptions` accepts an optional request-log sink; `None` uses the runtime
default.

The adapter keeps GenHTTP types at the boundary: the endpoint projection constructs the explicit environment the
workflow expects, so the workflow has no dependency on the server package.

## Reference

The package API reference covers endpoint construction, request decoding, response helpers, and endpoint options.
