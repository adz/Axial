---
title: "Axial.Flow: structured workflows"
linkTitle: Flow
description: Environment-aware workflows with typed errors, cancellation, scheduling, and structured concurrency.
type: docs
notoc: true
weight: 10
menu:
  main:
    weight: 6
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
<a class="docs-chip" href="{{< relref "/flow/getting-started.md" >}}">Getting started guide</a>
<a class="docs-chip" href="{{< relref "/reference/flow/" >}}">Flow API</a>
</div>
</div>

<section>
<span class="label" style="color:#6d4fc4">Everything coordinates through Flow</span>

<div class="axial-coord">

<div class="axial-coord-col axial-coord-col--left">
<span class="axial-coord-label">Your tools</span>
<div class="coord-row"><span class="coord-pill">Axial.Schema</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">Axial.ErrorHandling</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">Axial.Validation</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">Axial.Refined</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">Your types</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">Other libraries</span><span class="coord-line"></span></div>
</div>

<div class="axial-coord-mid">
<div class="coord-hub">
<img src="/content/img/favicon-light.svg" alt="Axial" />
<span class="coord-hub-name">Axial.Flow</span>
</div>
</div>

<div class="axial-coord-col axial-coord-col--right">
<span class="axial-coord-label">Services &amp; runtimes</span>
<div class="coord-row"><span class="coord-line"></span><span class="coord-pill">HTTP</span></div>
<div class="coord-row"><span class="coord-line"></span><span class="coord-pill">Files</span></div>
<div class="coord-row"><span class="coord-line"></span><span class="coord-pill">Databases</span></div>
<div class="coord-row"><span class="coord-line"></span><span class="coord-pill">Messaging</span></div>
<div class="coord-row"><span class="coord-line"></span><span class="coord-pill">Browser APIs</span></div>
<div class="coord-row"><span class="coord-line"></span><span class="coord-pill">JS ecosystem</span></div>
</div>

</div>

<p class="axial-coord-caption">Bring your own types and libraries on one side; reach services and runtimes on the
other. Flow is the seam where structure meets execution &mdash; on .NET, NativeAOT, Fable, browser and server.</p>
</section>

<div style="max-width: 68ch;">

## Core Flow

- [Getting Started](./getting-started/) — the smallest flow, execution, `Exit`, and the environment.
- [Straightforward Examples](./basic-examples/) — quick, practical flows without full application setup.
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

## In Practice

- [Runnable Examples](./examples/) — executed during the docs build, mirrored back into the site.
- [Troubleshooting Types](./troubleshooting-types/) — the compiler errors that mean a wrapper boundary was crossed.
- Comparisons: [vs Effect-TS](./effect-ts-comparison/), [FSharpPlus integration](./fsharpplus-comparison/).

Flow is one of the three areas Axial consists of — each usable independently, all working together. If the code is
still pure, start in [Error Handling]({{< relref "/error-handling/" >}}) or [Schema]({{< relref "/schema/" >}})
instead; both work without Flow.

</div>

</div>
