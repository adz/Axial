---
title: "Axial.Flow: structured workflows"
linkTitle: Flow
description: Environment-aware workflows with typed errors, cancellation, scheduling, and structured concurrency.
weight: 10
menu:
  main:
    weight: 6
aliases:
  - /docs/flow/
  - /docs/core-model/
  - /docs/managing-dependencies/
  - /docs/state-concurrency/
---

<div class="docs-home-container axial-landing">

<div style="max-width: 68ch; padding-top: 3rem;">
<span class="eyebrow" style="color:#6d4fc4">Axial.Flow &middot; Effects</span>

<h1>Structured workflows without framework lock-in.</h1>

<div class="lede">
Once validated data has to touch the outside world, the usual fixes each solve half the problem.
<code>Async&lt;Result&lt;_,_&gt;&gt;</code> flattens the nesting but still leaves you hand-threading infrastructure
through every argument &mdash; connections, configuration, trace ids, cancellation tokens. Meanwhile retries, resource
cleanup, and background work each get ad-hoc code that leaks tasks and swallows exceptions.
</div>

<div class="lede">
<code>Flow&lt;'env, 'error, 'value&gt;</code> addresses all of it in one type: async execution, typed errors, and an
explicit environment channel. A workflow declares what it needs in <code>'env</code>; the environment is supplied once
at the boundary &mdash; live in production, a mock record in tests. And because a flow is a cold description rather
than a running task, the runtime owns cancellation, retry scheduling, resource scopes, and structured concurrency for
you.
</div>

<div class="docs-home-meta">
<a class="docs-home-cta" href="{{< relref "/flow/tutorials/" >}}">Get started &gt;</a>
<a class="docs-chip" href="{{< relref "/docs/start/getting-started.md" >}}">Getting started guide</a>
<a class="docs-chip" href="{{< relref "/reference/flow/" >}}">Flow API</a>
</div>
</div>

<div style="max-width: 68ch;">

## Core Flow

- [Semantics](./semantics/) — cold workflows, execution, and how Flow fits the Axial model.
- [Execution and Outcomes](./execution-and-outcomes/) — running flows and reading `Exit`.
- [Task and Async Interop](./task-async-interop/) — binding `Async`, `Task`, `ValueTask`, and attempt constructors.
- [Defects and Exceptions](./defects/) — typed failures, defects, interruption, and exception recovery.
- [Bind](./bind/) — bind-site error assignment and mapping inside `flow {}`.
- [Flow Tutorials](./tutorials/) — service, runtime, environment, and layer walkthroughs.

## Dependencies and Resources

- [Dependencies](./dependencies/) — records, services, layers, scopes, and host boundaries.
- [Explicit Services](./explicit-services/) — reusable service contracts with `IHas<'service>`.
- [Layers](./layers/) — building explicit environments.
- [Scopes and Resources](./scopes-and-resources/) — resource lifetime and cleanup.
- [Building a Base Runtime](./building-a-base-runtime/) — standard operational services.
- [Service Provider Boundaries](./service-provider-boundaries/) — deliberate `IServiceProvider` edges.

## State and Concurrency

- [Fibers](./fibers/) — background workflow execution.
- [Deferred and Semaphore](./deferred-semaphore/) — coordination primitives.
- [Ref](./ref/) — atomic mutable references.
- [Schedule](./schedule/) — retry and repeat policies.
- [STM](./stm/) — transactional memory.
- [Stream](./stream/) — effectful pull-based streams.

If the code is still pure, start in [Parse, don't validate](../parse/) instead — Flow is optional, and both doors
work without it.

</div>

</div>
