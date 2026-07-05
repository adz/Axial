---
title: "Services Http"
weight: 40
type: docs
---

This page shows the HTTP service package. `IHttp` is intentionally narrow: it models a workflow that needs to fetch a string from a URL without binding the workflow to a concrete `HttpClient` setup. For richer clients, define an app-specific service and keep Axial responsible for orchestration and failure handling.

## Service

- [`Flow.Http.IHttp`](./t-flow-http-ihttp.md): Provides asynchronous access to HTTP client operations.

## Helpers

- [`Flow.Http.Http.getString`](./m-flow-http-http-getstring.md): Sends a GET request through an explicit HTTP service and returns the response body.
- [`Flow.Http.Http.live`](./m-flow-http-http-live.md): Creates a live HTTP service backed by <a href="https://learn.microsoft.com/dotnet/api/system.net.http.httpclient">HttpClient</a>.
- [`Flow.Http.Http.layer`](./m-flow-http-http-layer.md): Builds a live HTTP service as a layer.
