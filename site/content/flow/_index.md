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
Application functions often need the same dependencies passed through several callers before they reach the code that
uses them. Async code also needs one owner for cancellation, resources, retries, and background work.
</div>

<div class="lede">
<code>Flow&lt;'env, 'error, 'value&gt;</code> puts async execution, expected failures, and required dependencies in one
type. The host supplies live dependencies once; tests supply a small record of fakes. The runtime owns cancellation,
retry scheduling, resource scopes, and child fibers.
</div>

<div class="docs-home-meta">
<a class="docs-home-cta" href="{{< relref "/flow/tutorials/" >}}">Get started &gt;</a>
<a class="docs-chip" href="{{< relref "/flow/getting-started.md" >}}">Getting started guide</a>
<a class="docs-chip" href="{{< relref "/flow/reference/flow/" >}}">Flow API</a>
<a class="docs-chip" href="{{< relref "/flow/comparisons/task-vs-flow-scenarios.md" >}}">Task vs Flow, seven scenarios</a>
</div>
</div>

<section>
<span class="label" style="color:#6d4fc4">Everything coordinates through Flow</span>

<div class="axial-coord">

<div class="axial-coord-col axial-coord-col--left">
<span class="axial-coord-label">Your tools</span>
<div class="coord-row"><span class="coord-pill">Axial.Schema</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">Axial.ErrorHandling</span><span class="coord-line"></span></div>
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

## Install

```sh
dotnet add package Axial.Flow
```

`Flow<'env, 'error, 'value>`, the `flow {}` builder, and the runtime live in this one package. Reach for a satellite
package only for the specific integration it wraps — `Axial.Flow.HttpClient`, `Axial.Flow.Console`,
`Axial.Flow.FileSystem`, `Axial.Flow.Process`, the .NET/Node/browser hosting packages, and telemetry packages are
optional; none is required to use `flow {}` or `App` itself.

```sh
dotnet add package Axial.Flow.HttpClient
```

## Core Flow

- [Getting Started](./getting-started/) — the smallest flow, execution, `Exit`, and the environment.
- [Application Lifecycle](./applications/) — root applications with `App.run`, `App.start`, and coordinated stop.
- [Straightforward Examples](./basic-examples/) — quick, practical flows without full application setup.
- [Semantics](./core-concepts/semantics/) — cold workflows, execution, and how Flow fits the Axial model.
- [Execution and Outcomes](./core-concepts/execution-and-outcomes/) — running flows and reading `Exit`.
- [Task and Async Interop](./core-concepts/task-async-interop/) — binding `Async`, `Task`, `ValueTask`, and attempt constructors.
- [Defects and Exceptions](./core-concepts/defects/) — typed failures, defects, interruption, and exception recovery.
- [Bind](./core-concepts/bind/) — bind-site error assignment and mapping inside `flow {}`.
- [Flow Tutorials](./tutorials/) — service, runtime, environment, and layer walkthroughs.

## Dependencies and Resources

- [Dependencies](./services-and-runtimes/dependencies/) — records, services, layers, scopes, and host boundaries.
- [Explicit Services](./services-and-runtimes/explicit-services/) — reusable service contracts with `IHas<'service>`.
- [Layers](./services-and-runtimes/layers/) — building explicit environments.
- [Scopes and Resources](./services-and-runtimes/scopes-and-resources/) — resource lifetime and cleanup.
- [Building a Base Runtime](./services-and-runtimes/building-a-base-runtime/) — standard operational services.
- [Service Provider Boundaries](./services-and-runtimes/service-provider-boundaries/) — deliberate `IServiceProvider` edges.
- [Hosting](./hosting/) — standalone .NET, Generic Host, Node, and browser application edges.
- [Packages and Platforms](./packages-and-platforms/) — package boundaries and .NET, Node, and browser support.

## State and Concurrency

- [Fibers](./concurrency/fibers/) — background workflow execution.
- [Deferred and Semaphore](./concurrency/deferred-semaphore/) — coordination primitives.
- [Ref](./concurrency/ref/) — atomic mutable references.
- [Schedule](./concurrency/schedule/) — retry and repeat policies.
- [STM](./concurrency/stm/) — transactional memory.
- [Stream](./concurrency/stream/) — effectful pull-based streams.

## In Practice

- [Runnable Examples](./examples/) — executed during the docs build, mirrored back into the site.
- [Troubleshooting Types](./core-concepts/troubleshooting-types/) — the compiler errors that mean a wrapper boundary was crossed.
- Comparisons: [vs Effect-TS](./comparisons/effect-ts-comparison/), [FSharpPlus integration](./comparisons/fsharpplus-comparison/).

Flow is one of Axial's three entry points. If the code is still pure, start in
[Validation]({{< relref "/error-handling/" >}}) or [Schema]({{< relref "/schema/" >}})
instead; both work without Flow.

</div>

</div>
