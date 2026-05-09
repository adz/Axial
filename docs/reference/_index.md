---
title: Reference
description: Module index for the public FsFlow API surface.
---

# API Reference

This categorical index covers the synchronous, async, and task workflow families plus the pure validation bridge.

This package groups the synchronous, async, and task workflow families plus the pure validation bridge:

- `Flow<'env, 'error, 'value>` and the [`Flow`](/reference/flow/) module
- `AsyncFlow<'env, 'error, 'value>` and the [`AsyncFlow`](/reference/asyncflow/) module
- `TaskFlow<'env, 'error, 'value>` and the [`TaskFlow`](/reference/taskflow/) module
- `ColdTask<'value>` and the [`ColdTask`](/reference/coldtask/) module
- [`Check`](/reference/check/) for pure `Result<'value, unit>` predicates
- [`Guard`](/reference/guard/) for bindable check-like sources and error remapping
- [`Validation`](/reference/validation/) for accumulated diagnostics
- the [`result {}`](/reference/result/) builder for fail-fast orchestration over standard `Result` values
- the CAPS request tokens [`Needs<'dep>`](/reference/caps/) and [`Env<'dep>`](/reference/caps/) / `Env<'dep, 'value>` for named
  capability boundaries
- support types that shape runtime [`logging`](/reference/asyncflow-runtime/) and [`retry`](/reference/asyncflow-runtime/) behavior
- the [`flow {}`](/reference/flow/builders-flow.md), [`asyncFlow {}`](/reference/asyncflow/builders-asyncflow.md), [`taskFlow {}`](/reference/taskflow/taskbuilders-taskflow.md), [`result {}`](/reference/result/builders-result.md), and [`validate {}`](/reference/validation/builders-validate.md) entry points

The builder types themselves stay below the surface. The families and their modules are the public story.

## Flow

[`Flow`](/reference/flow/) is the synchronous family:

- build from values, options, and results
- read the environment explicitly
- compose with `map`, `bind`, `tap`, and `orElse`
- collect lists and sequences
- bridge into [`AsyncFlow`](/reference/asyncflow/) or validation when needed

<div class="docs-grid">

<section class="docs-card">
<span class="label">Flow</span>
<h2>Sync boundaries</h2>
<p>The <code>Flow&lt;'env, 'error, 'value&gt;</code> type and <code>Flow</code> module cover synchronous composition, explicit environment reads, short-circuiting, and execution.</p>
<p>The next page expands the full member map, including creation helpers, composition helpers, environment access, traversals, runtime helpers, and interop points.</p>
<div class="docs-card-links">
<a href="/reference/flow/">Open the Flow page</a>
</div>
</section>

<section class="docs-card">
<span class="label">AsyncFlow</span>
<h2>Async boundaries</h2>
<p>The <code>AsyncFlow&lt;'env, 'error, 'value&gt;</code> type and <code>AsyncFlow</code> module cover async composition, <code>Async</code> interop, explicit environment reads, and execution.</p>
<p>The next page expands the full member map, including creation helpers, composition helpers, traversals, runtime helpers, and async bridges.</p>
<div class="docs-card-links">
<a href="/reference/asyncflow/">Open the AsyncFlow page</a>
</div>
</section>

<section class="docs-card">
<span class="label">TaskFlow</span>
<h2>Task boundaries</h2>
<p>The <code>TaskFlow&lt;'env, 'error, 'value&gt;</code> type and <code>TaskFlow</code> module cover task-native composition, explicit environment reads, cancellation, and execution.</p>
<p>The next pages expand the full member map for the task surface, including cold task helpers, runtime helpers, and async bridges.</p>
<div class="docs-card-links">
<a href="/reference/taskflow/">Open the TaskFlow page</a>
</div>
</section>

<section class="docs-card">
<span class="label">Check</span>
<h2>Pure validation</h2>
<p><code>FsFlow.Check</code> gives pure <code>Result&lt;_, unit&gt;</code> checks for booleans, options, value options, nulls, collections, equality, and strings.</p>
<p>The <code>Check</code> page expands the full member map and the bridge that turns unit failures into application errors.</p>
<div class="docs-card-links">
<a href="/reference/check/">Open the check page</a>
</div>
</section>

<section class="docs-card">
<span class="label">Guard</span>
<h2>Bindable guard sources</h2>
<p><code>FsFlow.Guard</code> keeps the source visible to the computation expression and packages the failure value alongside it so the CE can bind the source directly.</p>
<p>The <code>Guard</code> page expands the constructors for check-like, effectful, and error-bearing sources across <code>flow</code>, <code>asyncFlow</code>, <code>taskFlow</code>, <code>result</code>, and <code>validate</code>.</p>
<div class="docs-card-links">
<a href="/reference/guard/">Open the Guard page</a>
</div>
</section>

</div>

<div class="docs-stack">

<section class="docs-card">
<span class="label">AsyncFlow.Runtime</span>
<h3>Operational support</h3>
<p>These types and modules handle the "how" of execution: <code>LogEntry</code>, <code>LogLevel</code>, <code>RetryPolicy</code>, and the <code>AsyncFlow.Runtime</code> helpers for timeouts, sleep, logging, and cancellation.</p>
<div class="docs-card-links">
<a href="/reference/asyncflow-runtime/">Open the runtime page</a>
</div>
</section>

<section class="docs-card">
<span class="label">Entry points</span>
<h3>Workflow builders</h3>
<p>The <code>flow {}</code>, <code>asyncFlow {}</code>, <code>taskFlow {}</code>, <code>result {}</code>, and <code>validate {}</code> entry points are available, but the builder types themselves are treated as plumbing rather than headline API.</p>
<p>Use the builders for readable orchestration, and use the modules for the actual API surface.</p>
</section>

</div>

## AsyncFlow

[`AsyncFlow`](/reference/asyncflow/) follows the same shape as [`Flow`](/reference/flow/), but its execution model is async-first:

- lift from synchronous flows when the boundary stays the same
- lift from `Async` and `Async<Result<_, _>>` when the runtime already is async
- keep the same explicit environment model
- bridge to [`TaskFlow`](/reference/taskflow/) when the application boundary moves to tasks

## TaskFlow

[`TaskFlow`](/reference/taskflow/) follows the same shape as [`Flow`](/reference/flow/) and [`AsyncFlow`](/reference/asyncflow/), but its runtime is task-first:

- lift from [`Flow`](/reference/flow/) and [`AsyncFlow`](/reference/asyncflow/) when the outer boundary already carries them
- lift from `Task`, `Task<Result<_, _>>`, `ValueTask`, `ValueTask<Result<_, _>>`, and [`ColdTask`](/reference/coldtask/)
- keep the same explicit environment model
- preserve cancellation through the boundary

## Read the subpages

<div class="docs-grid">

<section class="docs-card">
<span class="label">Flow</span>
<h3><a href="/reference/flow/">Sync boundaries</a></h3>
<p>Type, module, composition, environment, and execution in the synchronous family.</p>
</section>

<section class="docs-card">
<span class="label">AsyncFlow</span>
<h3><a href="/reference/asyncflow/">Async boundaries</a></h3>
<p>The async family surface, including async bridges and the same explicit environment model.</p>
</section>

<section class="docs-card">
<span class="label">TaskFlow</span>
<h3><a href="/reference/taskflow/">Task boundaries</a></h3>
<p>The task family surface, including cold task helpers, task-native runtime helpers, and async bridges.</p>
</section>

<section class="docs-card">
<span class="label">Check</span>
<h3><a href="/reference/check/">Pure checks</a></h3>
<p>Pure predicates and the bridge to effectful error creation.</p>
</section>

<section class="docs-card">
<span class="label">Guard</span>
<h3><a href="/reference/guard/">Bindable guard sources</a></h3>
<p>Direct binding for check-like values and existing-error remapping.</p>
</section>

<section class="docs-card">
<span class="label">Validation</span>
<h3><a href="/reference/validation/">Accumulating validation</a></h3>
<p>The <code>Validation</code> type and <code>validate {}</code> builder for collecting diagnostics into a structured graph.</p>
</section>

<section class="docs-card">
<span class="label">result { }</span>
<h3><a href="/reference/result/">Fail-fast orchestration</a></h3>
<p>The <code>result {}</code> builder for short-circuiting workflows over standard <code>Result</code> values.</p>
</section>

<section class="docs-card">
<span class="label">AsyncFlow.Runtime</span>
<h3><a href="/reference/asyncflow-runtime/">Operational support</a></h3>
<p><code>AsyncFlow.Runtime</code> covers logging, retry helpers, cancellation, and async runtime operations like timeouts and sleep.</p>
</section>

</div>
