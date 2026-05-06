---
title: Capability
description: Source-documented capabilities and layers for FsFlow.
---

# Capability

This page shows the source-documented capability and layer surface, used for dependency injection and environment management in task workflows.

## Capabilities

- module [`Capability`](./capability.md): Capability helpers for record-based environments and .NET service-provider interop. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L813)
- type [`MissingCapability`](./missingcapability.md): Describes a missing service-provider capability. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L147)
- [`Capability.service`](./capability-service.md): Reads a capability from a record-based environment projection. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L815)
- [`Capability.runtime`](./capability-runtime.md): Reads a capability from the runtime half of a two-context runtime environment. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L820)
- [`Capability.environment`](./capability-environment.md): Reads a capability from the application half of a two-context runtime environment. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L826)
- [`Capability.serviceFromProvider`](./capability-servicefromprovider.md): Reads a service from `IServiceProvider` and fails when it is not registered. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L832)

## Layers

- module `Layer`: Helpers for deriving an environment in one flow and consuming it in another. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L846)
- [`Layer.provideLayer`](./layer-providelayer.md): Provides a derived environment from a layer flow to a downstream flow. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L848)

