---
title: "Service"
weight: 140
---

This page shows the service helpers around FsFlow's explicit environment model. In FsFlow, a service is a named dependency contract such as `IClock`, `IConsole`, or `IHttp`. Prefer plain records plus `Flow.read` for local workflow code, use `IHas<'T>` plus `Service<'service>.get()` when reusable helpers need a nominal service contract, and keep `Service<'service>.resolve()` at .NET host boundaries where `IServiceProvider` interop is useful. Layers provision explicit services, while the ambient runtime is reserved for closed executor mechanics only.

See the standard service packages: [Core](./core/), [Console](./console/), [FileSystem](./filesystem/), [Http](./http/), and [Process](./process/).

## Service contracts

- [`IHas`](./t-ihas.md): Nominal contract for an explicit service dependency.
- [`Service`](./t-service.md): Typed accessors for explicit and provider-resolved services.

## Service accessors

- [`Service.get`](./m-service-get.md): Reads a statically declared service from an environment that implements <code>IHas&lt;&#39;service&gt;</code>.
- [`Service.resolve`](./m-service-resolve.md): Resolves a service dynamically from an <a href="https://learn.microsoft.com/dotnet/api/system.iserviceprovider">IServiceProvider</a> environment.

## Environment helpers

- [`Flow.read`](./m-flow-read.md): Projects one value from the current environment.

