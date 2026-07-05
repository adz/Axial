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

Once validated data has to touch the outside world, plain `Result` stops being enough — and the usual fixes each solve
half the problem. `Async<Result<_,_>>` and `Task<Result<_,_>>` flatten the nesting but still leave you manually
plumbing infrastructure through every function argument: database connections, configuration, trace ids, cancellation
tokens. Signatures bloat, or worse, the dependencies go global. Meanwhile cancellation, retries, resource cleanup, and
background work are each handled with ad-hoc code that leaks tasks and swallows exceptions.

`Flow<'env, 'error, 'value>` addresses all of it in one type. It combines async execution, typed error tracking, and
an explicit environment channel: a workflow declares what it needs in `'env`, and the environment is supplied once at
the application boundary instead of threaded through every call — which also makes swapping a live environment for a
test one trivial. Because a flow is a cold description of work rather than a running task, the runtime can own
cancellation, retry scheduling, resource scopes, and structured concurrency (fibers) for you.

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
