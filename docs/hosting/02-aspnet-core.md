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
error response mapper. `Request.json`, `Request.form`, and `Request.query` decode the request through a Reified schema;
`Response` and `SchemaResult` write typed responses. `EndpointOptions` accepts the request-log sink; `None` leaves the
runtime default.

The host-facing shape keeps the application environment at the edge: the endpoint projection constructs the same
explicit record or layer that the workflow already expects, and the workflow remains independent of ASP.NET Core types.

## Reference

The package API reference covers endpoint construction, request decoding, response helpers, and endpoint options. See
[running as a service](/hosting/running-as-a-service.html) for the process-level lifecycle around the web host.
