---
title: "Capability"
---

This page shows the capability and layer helpers around FsFlow's environment model. In FsFlow, a capability is a named interface that describes what a flow needs from `env`; the workflow still receives an explicit environment, but the interface gives that dependency surface a stable name. Prefer small app contracts plus `Flow.read` for reusable workflow code. Keep runtime-owned services such as clock, logging, random, GUID generation, and environment-variable lookup in `FsFlow.Capabilities.Core`, where they can be read through runtime helpers and overridden with `Flow.withClock`, `Flow.withLog`, `Flow.withRandom`, `Flow.withGuid`, and `Flow.withEnvironmentVariables`. The `Resolver` helpers are edge and compatibility tools, not the default application model.

## Binding tokens

- [`Requires`](./t-requires-1.md): Compatibility contract for a single dependency.
- [`Resolve`](./t-resolve-1.md): Request token for binding a whole dependency inside a workflow.
- [`Resolve`](./t-resolve-2.md): Request token for projecting a value from a dependency.

## Edge helpers

- [`MissingCapability`](./t-missingcapability.md): Describes a missing service-provider capability.
- [`Resolver.resolve`](./m-resolver-resolve.md): Reads a dependency from the environment using the provided projection.
- [`Resolver.fromProvider`](./m-resolver-fromprovider.md): Reads a dependency from <a href="https://learn.microsoft.com/dotnet/api/iserviceprovider">IServiceProvider</a> and fails when it is not registered.

## Layers

- [`Layer.provideLayer`](./m-layer-providelayer.md): Provides a derived environment from a layer flow to a downstream flow.

