---
weight: 40
title: Flow
type: docs
description: Effectful workflows with environment, async and task interop, layers, resources, and concurrency.
aliases:
  - /docs/core-model/
  - /docs/managing-dependencies/
  - /docs/state-concurrency/
---

# Flow

Flow covers effectful workflow boundaries: environment access, async and task work, cancellation, resources, scheduling, and concurrency.

Use `Flow` when pure `Result` or `Validation` no longer describes the work. If the code is still pure, start with [Error Handling](../error-handling/) or [Validation](../validation/) instead.

## Mental Model

Start with the smallest useful signature:

```fsharp
Flow<'value>
Flow<'error, 'value>
EnvFlow<'env, 'value>
Flow<'env, 'error, 'value>
```

Use the full `Flow<'env, 'error, 'value>` form when a workflow needs both an environment and typed failures.

## Core Flow

- [Semantics](./semantics/): cold workflows, execution, and how Flow fits the Axial model.
- [Execution and Outcomes](./execution-and-outcomes/): running flows and reading `Exit`.
- [Task and Async Interop](./task-async-interop/): binding `Async`, `Task`, `ValueTask`, and attempt constructors.
- [Defects and Exceptions](./defects/): typed failures, defects, interruption, and exception recovery.
- [Bind](./bind/): bind-site error assignment and mapping inside `flow {}`.
- [Flow Tutorials](./tutorials/): service, runtime, environment, and layer walkthroughs.

## Dependencies and Resources

- [Dependencies](./dependencies/): records, services, layers, scopes, and host boundaries.
- [Explicit Services](./explicit-services/): reusable service contracts with `IHas<'service>`.
- [Layers](./layers/): building explicit environments.
- [Scopes and Resources](./scopes-and-resources/): resource lifetime and cleanup.
- [Building a Base Runtime](./building-a-base-runtime/): standard operational services.
- [Service Provider Boundaries](./service-provider-boundaries/): deliberate `IServiceProvider` edges.

## State and Concurrency

- [Fibers](./fibers/): background workflow execution.
- [Deferred and Semaphore](./deferred-semaphore/): coordination primitives.
- [Ref](./ref/): atomic mutable references.
- [Schedule](./schedule/): retry and repeat policies.
- [STM](./stm/): transactional memory.
- [Stream](./stream/): effectful pull-based streams.

## Reference

- [Flow API]({{< relref "/reference/flow/" >}})
- [Layer API]({{< relref "/reference/layer/" >}})
- [Service API]({{< relref "/reference/service/" >}})
- [Concurrency API]({{< relref "/reference/concurrency/" >}})
