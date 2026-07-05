---
title: "Axial.Flow: structured workflows"
linkTitle: Flow
description: Structured workflows without framework lock-in.
menu:
  main:
    weight: 6
---

<div class="docs-home-container axial-landing">

<div class="docs-home-hero">

<div class="docs-home-copy">
<span class="eyebrow" style="color:#6d4fc4">Axial.Flow &middot; Effects</span>

<h1>Structured workflows without framework lock-in.</h1>

<div class="lede">
A cold, environment-aware Reader-Async-Result workflow model in the ZIO tradition. Compose services, async and task
work, cancellation, and runtime policy &mdash; with dependencies visible in the type, not hidden in a container.
</div>

<div class="docs-home-meta">
<a class="docs-home-cta" href="{{< relref "/docs/flow/tutorials/" >}}">Get started &gt;</a>
<a class="docs-chip" href="{{< relref "/docs/flow/" >}}">Flow guides</a>
<a class="docs-chip" href="{{< relref "/docs/patterns/examples/" >}}">Examples</a>
</div>
</div>

<div class="axial-hero-panels">

<div class="axial-panel">
<span class="label">Flow example</span>

```fsharp
let readVerifiedEmail userId =
    flow {
        let! loadUser = Flow.read _.LoadUser   // dependency from 'env
        let! user = loadUser userId            // Task<Result<_,_>> binds directly
        return! validateEmail user.Email |> Flow.fromResult
    }

// nothing runs until you execute it
readVerifiedEmail 42 |> Flow.runSync appEnv
```

</div>

<div class="axial-panel">
<span class="label">Flow variants (conceptual)</span>
<div class="axial-variants">

| Shape | Meaning |
| :--- | :--- |
| `Flow<'env, 'err, 'value>` | Full power: environment + typed failure |
| `Flow<'err, 'value>` | No environment |
| `Flow<'value>` | Simplest form: cannot fail |
| `ExnFlow<'value>` | Recoverable exceptions in the error channel |

</div>
</div>

</div>

</div>

<ul class="axial-checks axial-checks--flow">
<li>Explicit dependencies and control flow stay visible</li>
<li>Cancellation and runtime policy built in</li>
<li>Binds <code>Task</code>, <code>ValueTask</code>, <code>Async</code>, <code>Result</code>, and <code>Option</code> directly</li>
<li>Fibers, STM, streams, and scheduling when you need them</li>
<li>Runs on .NET, NativeAOT, Fable, browser and server</li>
</ul>

<section>
<span class="label">What you get</span>

<div class="docs-grid">

<section class="docs-card">
<h3>Explicit workflows</h3>
<p>Service requirements live in <code>'env</code>: records and <code>Flow.read</code> for application code,
<code>Service&lt;'T&gt;.get()</code> for named contracts, provider interop only at the host edge.</p>
</section>

<section class="docs-card">
<h3>Layers and scoped resources</h3>
<p><code>layer { }</code> builds environments with owned cleanup; <code>use!</code>,
<code>Flow.acquireRelease</code>, and finalizers tie resource lifetime to scope.</p>
</section>

<section class="docs-card">
<h3>Concurrency</h3>
<p>Structured fibers with <code>fork</code>/<code>join</code>/<code>interrupt</code>, parallel composition with
<code>zipPar</code> and <code>race</code>, lock-free coordination with STM.</p>
</section>

<section class="docs-card">
<h3>Scheduling and outcomes</h3>
<p><code>Schedule.retry</code> and <code>Schedule.repeat</code> for policy, and an <code>Exit</code> model that keeps
typed failures, interruptions, and defects distinct.</p>
</section>

</div>
</section>

<div class="axial-strip axial-strip--parse">
<p>
Parsing at the boundary? One schema turns raw form, CLI, JSON, or configuration input into trusted models &mdash; then
<code>Flow.verify</code> carries the result into your workflow.
</p>
<a href="{{< relref "/parse/" >}}">Parse, don't validate &rarr;</a>
</div>

</div>
