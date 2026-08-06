---
title: "Axial: structured workflows"
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

<div class="docs-home-hero">

<div class="docs-home-copy">
<section>
<span class="label" style="color:#6d4fc4">Everything coordinates through Flow</span>

<div class="axial-coord">

<div class="axial-coord-col axial-coord-col--left">
<span class="axial-coord-label">Your tools</span>
<div class="coord-row"><span class="coord-pill">Axial.Schema</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">Axial.Result</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">Your types</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">Other libraries</span><span class="coord-line"></span></div>
</div>

<div class="axial-coord-mid">
<div class="coord-hub">
<img src="/content/img/favicon-light.svg" alt="Axial" />
<span class="coord-hub-name">Axial</span>
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

<span class="eyebrow" style="color:#6d4fc4">Axial &middot; Effects</span>

<h1>A type, not a framework.</h1>

<div class="lede">
It fits your app, not the other way around. Without it, dependencies get threaded through call after call by hand,
and cancellation, retries, and cleanup get scattered across whatever async code happens to touch them.
</div>

<div class="lede">
<code>Flow&lt;'env, 'error, 'value&gt;</code> puts async execution, expected failures, and required dependencies in one
type. The host supplies live dependencies once; tests supply a small record of fakes. The runtime owns cancellation,
retry scheduling, resource scopes, and child fibers.
Add packages that take advantage of these abilities for platform
services, HTTP, processes, telemetry, and .NET, Node, or browser hosting.
</div>

<div class="docs-home-meta">
<a class="docs-home-cta" href="{{< relref "/flow/tutorials/" >}}">Get started &gt;</a>
<a class="docs-chip" href="{{< relref "/flow/getting-started/" >}}">Getting started guide</a>
<a class="docs-chip" href="{{< relref "/flow/reference/flow/" >}}">Flow API</a>
<a class="docs-chip" href="{{< relref "/flow/comparisons/task-vs-flow-scenarios.md" >}}">Task vs Flow, seven scenarios</a>
</div>
</div>

</div>

<div style="max-width: 68ch;">

## Packages

`Axial` contains the workflow model and runtime. The other packages add one platform service, host, or telemetry
integration at a time.

| Package | Use it for | Documentation |
| --- | --- | --- |
| `Axial` | Workflows, environments, typed failures, concurrency, and runtime execution | [Axial](./overview/) |
| `Axial.PlatformService` | Shared clock and platform service contracts | [Platform services](./platform-service/) |
| `Axial.Console` | Console input and output | [Console](./console/) |
| `Axial.FileSystem` | Filesystem operations | [FileSystem](./filesystem/) |
| `Axial.HttpClient` | HTTP client requests | [HTTP client](./http/) |
| `Axial.Process` | Child processes | [Processes](./processes/) |
| `Axial.Hosting` | .NET application hosting | [Hosting](./hosting/) |
| `Axial.Hosting.Node` | Node application hosting | [Node hosting](./hosting/node/) |
| `Axial.Hosting.Browser` | Browser application hosting | [Browser hosting](./hosting/browser/) |
| `Axial.Telemetry` | Runtime telemetry contracts | [Telemetry](./telemetry/) |
| `Axial.Telemetry.JavaScript` | JavaScript telemetry integration | [JavaScript telemetry](./telemetry/javascript/) |

Flow is one of Axial's three entry points. If the code is still pure, start in
[Result]({{< relref "/result/" >}}), [Values]({{< relref "/values/" >}}), or [Schema]({{< relref "/schema/" >}})
instead; both work without Flow.

See [Axial](./overview/) for the Flow type, getting started, dependencies, concurrency, and the full guide list.

</div>

</div>
