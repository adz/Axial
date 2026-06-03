# FsFlow Plan

This file tracks current product and architecture direction.
Settled historical decisions live in `dev-docs/decisions/`.
Historical dependency-model research lives in `dev-docs/deprecated/caps-research/`, but this file is the live direction.

## Current Direction

FsFlow keeps the public workflow type simple:

```fsharp
Flow<'env, 'error, 'value>
```

The active direction now splits concerns like this:

- explicit services and app/domain dependencies live in `'env`
- executor mechanics live in a closed ambient runtime

This is a stricter model than the earlier ambient-services plan. First-party service packages and former
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
- `Flow.provide` is the main way to run flows with a built environment
- `layer { }` is the primary app-environment construction style: `let!` is dependent/sequential, while sibling `and!`
  uses `Layer.merge` / `Layer.zipPar` for independent provisioning
- `Layer.merge` combines layer outputs as tuples; it does not synthesize records, interfaces, or automatic
  `IHas<'service>` environments
- `Flow.acquireReleaseWith` is the local acquire/use/release combinator
- `Flow.acquireRelease` attaches acquired resources to the current runtime scope
- `Layer.acquireRelease` attaches provisioned service resources to the layer scope

The internal registry has been removed rather than promoted.

FsFlow v1 intentionally does not add tagged services or automatic service-environment merging. Multiple services of the
same type should be modeled with explicit named record fields or distinct nominal contracts. If boilerplate becomes a
real problem, prefer a future source generator that emits named environment records and `IHas<'service>` implementations
over reflection, proxy types, or hidden service maps.

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

- presenting first-party services as magical ambient runtime slots
- presenting registry-backed runtime as the architecture
- teaching `IHasX` as the default for every dependency
- centering `IServiceProvider` as the main app model

## Implementation Snapshot

As of 2026-06-03, core code, service packages, tests, examples, and generated reference docs use the explicit
service/layer model. Integration tests cover `Microsoft.Extensions.DependencyInjection` provider-backed base runtime
construction, typed missing-registration failures, direct `Service<'T>.resolve()` defects, and composition of the
current Console, FileSystem, Http, and Process service layers. Remaining work should improve public guide coverage and
future service packages, not preserve the old ambient-core or `Flow.service` / `Flow.inject` direction.

## Open Product Questions

- Should future telemetry services live under `FsFlow.Services.Telemetry` or stay as runtime instrumentation adapters?
- Should process support add scoped long-running process helpers beyond one-shot `Process.execute`?
