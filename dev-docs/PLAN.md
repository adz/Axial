# FsFlow Plan

This file tracks current product and architecture direction.
Settled historical decisions live in `dev-docs/decisions/`.
Historical capability research lives in `dev-docs/deprecated/caps-research/`, but this file is the live direction.

## Current Direction

FsFlow keeps the public workflow type simple:

```fsharp
Flow<'env, 'error, 'value>
```

The active direction now splits concerns like this:

- explicit services and app/domain dependencies live in `'env`
- executor mechanics live in a closed ambient runtime

This is a stricter model than the earlier ambient-capabilities plan. First-party service packages and former
ambient operational services should be expressed as explicit services, not runtime slots.

## Active Architecture

### Explicit Environment

`'env` is for:

- repositories
- gateways
- domain services
- feature dependencies
- request or user context when it is part of business logic
- operational services modeled explicitly as services

The default access pattern is:

```fsharp
Flow.read (fun env -> env.Orders)
```

Plain records are still the default recommendation for local app code. They are simple, legible, easy to test, and
avoid unnecessary service boilerplate.

### Nominal Service Contracts

`IHas<'service>` and `Service<'service>.get()` are the nominal compile-time checked service story.

Use this when the static contract is worth the ceremony:

```fsharp
type IHasOrders = inherit IHas<IOrderRepository>

let saveOrder order : Flow<#IHasOrders, OrderError, unit> =
    flow {
        let! orders = Service<IOrderRepository>.get()
        return! orders.Save order
    }
```

Do not make `IHasX` the default story for all dependencies. For most feature-local code, records plus `Flow.read` are
better.

### Provider Edge

`Service<'service>.resolve()` exists for pragmatic .NET host integration when `'env :> IServiceProvider`.

Use it at host edges, glue code, prototypes, and boundary adapters. Prefer mapping provider registrations into an
explicit record or nominal contract before entering core domain workflows.

Missing provider registrations are configuration defects, not domain errors.

### Closed Ambient Runtime

Ambient runtime state is now reserved for executor mechanics only. It is not the dependency model for first-party
service packages.

Ambient mechanics include:

- cancellation token access
- scope ownership
- scheduling and interruption helpers
- runtime annotations and trace metadata

Former ambient operational services such as clock, log, random, GUID, and environment variables should be modeled as
explicit services and provisioned through environments and layers.

## Scope And Layers

`Scope` and `Layer` are now part of the target public architecture rather than deferred internals.

- `Scope` owns deterministic teardown
- `Layer` provisions explicit environments and service bundles
- `Flow.provide` should become the main way to run flows with a built environment

The internal registry should be removed rather than promoted.

## Service Packages

Service packages should focus on explicit, typed, testable system effects:

- Core: clock, log, random, GUID, environment variables
- Console
- FileSystem
- Http
- Process
- future Network and telemetry packages

Service-package operations should use explicit services. They should normally be thin wrappers over
`Service<'service>.get()` plus live implementations and layers.

## Documentation Direction

Public docs should teach this order:

1. Use plain records plus `Flow.read` for most app dependencies.
2. Use `IHas<'T>` plus `Service<'service>.get()` for reusable named service contracts.
3. Use `Service<'service>.resolve()` at .NET host edges.
4. Use `Layer` to provision environments and base runtime bundles.
5. Treat the ambient runtime as executor mechanics only.

Docs must avoid:

- presenting first-party capabilities as magical ambient runtime slots
- presenting registry-backed runtime as the architecture
- teaching `IHasX` as the default for every dependency
- centering `IServiceProvider` as the main app model

## Transition Snapshot

As of this plan, the repository still contains code and docs from the earlier ambient-core and `Flow.service` /
`Flow.inject` direction. Those are migration targets, not the desired v1 endpoint.

## Open Product Questions

- What is the final minimal public `Layer` surface?
- Do we ship a standard `BaseRuntime` bundle in core, hosting, or a dedicated package?
- Which scope helpers should be public beyond raw finalizer registration?
- What is the best public naming for `Flow.provide` versus any narrower helpers derived from layers?
