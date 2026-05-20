# FsFlow Plan

This file tracks current product and architecture direction.
Settled historical decisions live in `dev-docs/decisions/`.
Capability research lives in `dev-docs/caps-research/`, but this file is the live direction.

## Current Direction

FsFlow keeps the public workflow type simple:

```fsharp
Flow<'env, 'error, 'value>
```

The active design splits dependencies into two groups:

- app/domain dependencies live in the explicit user environment, `'env`
- runtime/system services are ambient runtime services and do not appear in `'env`

This means ordinary workflow signatures should advertise business requirements, not every operational service used
for logging, time, random values, GUID generation, environment variables, timeout, retry, cancellation, or cleanup.

## Active Architecture

### Explicit Environment

`'env` is for app and domain dependencies:

- repositories
- gateways
- domain services
- feature dependencies
- request or user context when it is part of business logic

The default access pattern is:

```fsharp
Flow.read (fun env -> env.Orders)
```

Plain records are the default recommendation for local app code. They are simple, legible, easy to test, and avoid
unnecessary capability boilerplate.

### Nominal App Contracts

`IHas<'service>` and `Flow.service<'service>()` are available for reusable helpers that benefit from a named,
compile-time checked app dependency contract.

Use this when the static contract is worth the ceremony:

```fsharp
type IHasOrders = inherit IHas<IOrderRepository>

let saveOrder order : Flow<#IHasOrders, OrderError, unit> =
    flow {
        let! orders = Flow.service<IOrderRepository, _, _>()
        return! orders.Save order
    }
```

Do not make `IHasX` the default story for all domain dependencies. For most feature-local code, records are better.

### Provider Edge

`Flow.inject<'service>()` exists for pragmatic .NET host integration when `'env :> IServiceProvider`.

Use it at host edges, glue code, prototypes, and boundary adapters. Prefer mapping provider registrations into a typed
record or nominal contract before entering core domain workflows.

Missing provider registrations are configuration defects, not domain errors.

### Ambient Runtime

Runtime services are stored internally in a fixed runtime context and carried by the execution engine with ambient
state. These services do not appear in `Flow<'env, 'error, 'value>` signatures.

Current runtime services:

- `IClock`
- `ILog`
- `IRandom`
- `IGuid`
- `IEnvironmentVariables`
- cancellation token access
- scheduling helpers such as sleep, retry, and timeout
- resource helpers such as acquire/use/release

Public access goes through `Flow.Runtime` or capability-family helpers:

```fsharp
open FsFlow.Capabilities.Core

let workflow =
    flow {
        let! now = Clock.now
        do! Log.info $"Started at {now}"
        let! id = Guid.newGuid
        return id
    }
```

Overrides are local to a flow subtree:

```fsharp
workflow
|> Flow.withClock fakeClock
|> Flow.withLog testLog
```

This is the active model. Runtime services are ambient but overridable, not part of the end-user app environment.

## Deferred Registry / Scope / Layer Work

The repository currently contains internal `Registry`, `Scope`, `RuntimeAdapter`, and `RuntimeLayer` modules with
tests. They are not the active runtime storage engine for normal workflow execution.

Treat them as deferred foundation pieces for a more complete scope/layer system, not as the current architecture.

Do not write user-facing docs that claim FsFlow uses a registry-backed runtime today.
Do not make registry adoption a prerequisite for the current capability model.

Registry/scope/layer work becomes active only if we decide to implement richer behavior such as:

- dynamically extensible runtime service families
- tagged runtime services
- deterministic scope ownership across composed layers
- resource-producing layers with teardown guarantees
- registry-backed host provisioning

Until then, the fixed ambient runtime is the source of truth.

## Capability Families

Capability packages should focus on explicit, typed, testable system effects:

- Core: clock, log, random, GUID, environment variables
- Console
- FileSystem
- Http
- Process
- future context or telemetry packages

Capability-family operations should normally read ambient runtime services or use their own package-specific runtime
bridge. They should not force every operation into user `'env` unless the dependency is genuinely app/domain owned.

## Documentation Direction

Public docs should teach this order:

1. Use plain records plus `Flow.read` for most app dependencies.
2. Use ambient runtime helpers for operational services.
3. Use `IHas<'T>` plus `Flow.service` for reusable strict app contracts.
4. Use `Flow.inject` at .NET host edges.
5. Treat registry/scope/layer internals as implementation or future architecture, not current user guidance.

Docs must avoid:

- presenting registry-backed runtime as implemented public behavior
- presenting `RuntimeContext<'runtime, 'env>` as the public model
- teaching `IHasX` as the default for every repository or domain service
- mixing runtime/system effects into ordinary app environment signatures

## Implementation Snapshot

As of this plan:

- `Flow<'env, 'error, 'value>` is the public workflow type.
- `Flow.run` installs `RuntimeContext.live` into ambient runtime state.
- `Flow.withClock`, `Flow.withLog`, `Flow.withRandom`, `Flow.withGuid`, and `Flow.withEnvironmentVariables` override runtime state for a flow subtree.
- `Flow.Runtime.now`, `Flow.Runtime.log`, `Flow.Runtime.newGuid`, `Flow.Runtime.nextInt`, and `Flow.Runtime.tryGetEnvironmentVariable` read ambient runtime state.
- `FsFlow.Capabilities.Core` aliases the core runtime service interfaces and exposes helpers over `Flow.Runtime`.
- `Flow.read`, `Flow.service`, and `Flow.inject` are the active app dependency accessors.
- `Registry`, `Scope`, `RuntimeAdapter`, and `RuntimeLayer` are internal and currently exercised by foundation tests only.

## Open Product Questions

- Should registry/scope/layer remain in `src/FsFlow` as dormant internal infrastructure, or move behind a clearer
  experimental boundary until it is connected to public behavior?
- Should `Flow.inject` return a typed missing-capability error instead of defecting, or is missing DI registration
  correctly treated as a configuration defect?
- Should capability packages beyond Core use the ambient runtime directly, or should each package expose explicit
  override helpers like `Flow.withFileSystem` when they mature?
- How much of `IHas<'T>` should be emphasized in public docs now that records are the default app dependency model?
