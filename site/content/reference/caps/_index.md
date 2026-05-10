---
title: "CAPS"
type: docs
weight: 130
---

This page shows the source-documented capability and layer surface, including the CAPS request tokens, named capability helpers, and layer composition used for environment management in task workflows.

## CAPS tokens

- type [`Needs`](./needs.md): Describes the capability contract for a single dependency. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L98)
- type [`Env`](./env.md): Request token for binding a whole dependency inside a workflow. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L125)
### Constructors

- `Env` [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L126)


## Capabilities

- module [`Capability`](./capability.md): Capability helpers for record projections, runtime adapters, and .NET service-provider interop. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L1805)
- type [`MissingCapability`](./missingcapability.md): Describes a missing service-provider capability. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L288)
- [`Capability.service`](./capability-service.md): Reads a capability from a record-based environment projection. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L1811)
- [`Capability.runtime`](./capability-runtime.md): Reads a capability from the runtime half of a two-context runtime environment. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L1816)
- [`Capability.environment`](./capability-environment.md): Reads a capability from the application half of a two-context runtime environment. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L1822)
- [`Capability.serviceFromProvider`](./capability-servicefromprovider.md): Reads a service from `IServiceProvider` and fails when it is not registered. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L1828)

## Layers

- module `Layer`: Helpers for deriving an environment in one flow and consuming it in another. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L1841)
- [`Layer.provideLayer`](./layer-providelayer.md): Provides a derived environment from a layer flow to a downstream flow. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L1843)

