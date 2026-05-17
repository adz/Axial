---
title: "Capabilities Http"
---

This page shows the HTTP capability package. `IHttp` is intentionally narrow: it models a workflow that needs to fetch a string from a URL without binding the workflow to a concrete `HttpClient` setup. Use it for small integrations and examples where a single `getString` operation is enough. For production clients with richer behavior, define an app-specific interface and keep FsFlow responsible for orchestration, typed failure, and environment threading.

## Capability

- [`Capabilities.Http.IHttp`](./t-capabilities-http-ihttp.md): Provides asynchronous access to HTTP client operations.

## Helpers

- [`Capabilities.Http.Http.getString`](./m-capabilities-http-http-getstring.md): Sends a GET request using the HTTP environment and returns the response body.
- [`Capabilities.Http.Http.live`](./m-capabilities-http-http-live.md): Creates a live HTTP client backed by <a href="https://learn.microsoft.com/dotnet/api/system.net.http.httpclient">HttpClient</a>.

