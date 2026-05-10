---
title: "Capability"
---

This page shows the source-documented capability and layer surface, including CAPS request tokens and environment management helpers.

## CAPS tokens

- [`FsFlow.Needs`](./t-needs.md): Describes the capability contract for a single dependency.
- [`FsFlow.Env`](./t-env.md): Request token for binding a whole dependency inside a workflow.
- [`FsFlow.Env`](./t-env.md): Request token for projecting a value from a dependency.

## Capabilities

- [`FsFlow.MissingCapability`](./t-missingcapability.md): Describes a missing service-provider capability.
- [`FsFlow.CapabilityModule.service`](./m-capabilitymodule-service.md): Reads a capability from a record-based environment projection.
- [`FsFlow.CapabilityModule.runtime`](./m-capabilitymodule-runtime.md): Reads a capability from the runtime half of a two-context runtime environment.
- [`FsFlow.CapabilityModule.environment`](./m-capabilitymodule-environment.md): Reads a capability from the application half of a two-context runtime environment.
- [`FsFlow.CapabilityModule.serviceFromProvider`](./m-capabilitymodule-servicefromprovider.md): Reads a service from `IServiceProvider` and fails when it is not registered.

## Layers

- [`FsFlow.LayerModule.provideLayer`](./m-layermodule-providelayer.md): Provides a derived environment from a layer flow to a downstream flow.

