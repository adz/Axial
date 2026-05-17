---
title: "Capability"
weight: 130
type: docs
---

This page shows the capability helpers around FsFlow's environment model. In FsFlow, a capability is a named interface that describes what a flow needs from `env`; the workflow still receives an explicit environment, but the interface gives that dependency surface a stable name. Prefer plain records plus `Flow.read` for local workflow code, use `IHas<'T>` plus `Flow.service` when reusable helpers need statically checked dependency contracts, and keep `Flow.inject` at .NET host boundaries where `IServiceProvider` interop is useful. Runtime-owned services such as clock, logging, random, GUID generation, and environment-variable lookup stay in `FsFlow.Capabilities.Core`, where they can be read through runtime helpers and overridden with `Flow.withClock`, `Flow.withLog`, `Flow.withRandom`, `Flow.withGuid`, and `Flow.withEnvironmentVariables`. 

See the standard capability packages: [Core](./core/), [Console](./console/), [FileSystem](./filesystem/), [Http](./http/), and [Process](./process/).

## Capability contracts

- [`IHas`](./t-ihas.md): Compatibility contract for a single dependency.

## Flow accessors

- [`Flow.read`](./m-flow-read.md): Projects one value from the current environment.
- [`Flow.service`](./m-flow-service.md): Extracts a specific service from an environment that implements <code>IHas&lt;&#39;service&gt;</code>.
- [`Flow.inject`](./m-flow-inject.md): Injects a service from a dynamic IServiceProvider environment.

## Edge helpers

- [`MissingCapability`](./t-missingcapability.md): Describes a missing service-provider capability.

