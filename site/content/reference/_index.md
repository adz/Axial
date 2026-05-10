---
title: Reference
description: Module index for the public FsFlow API surface.
type: docs
weight: 30
---

# API Reference

This categorical index covers the synchronous workflow surface, the capability helpers, the pure validation bridge, and the CAPS support packages.

This package groups the main public surfaces:

- `Flow<'env, 'error, 'value>` and the [`Flow`](/reference/flow/) module
- [`Capability`](/reference/capability/) for named capability helpers and environment access
- [`Check`](/reference/check/) for pure `Result<'value, unit>` predicates
- [`Validation`](/reference/validation/) for accumulated diagnostics
- [`Diagnostics`](/reference/diagnostics/) for the structured validation graph
- the [`flow {}`](/reference/flow/builders-flow.md) and [`validate {}`](/reference/validation/builders-validate.md) entry points
- the CAPS request tokens [`Needs<'dep>`](/reference/caps/) and [`Env<'dep>`](/reference/caps/) / `Env<'dep, 'value>` for explicit capability boundaries
- the [`FsFlow.Caps.Core`](/reference/caps-core/) package for the shared clock, random, GUID, and environment-variable primitives
- the [`FsFlow.Caps.Context`](/reference/caps-context/) package for request, user, locale, metadata, and request-flag context

The builder types themselves stay below the surface. The flow, capability, and validation modules are the public story.

## Flow

[`Flow`](/reference/flow/) is the synchronous workflow family:

- build from values, options, and results
- read the environment explicitly
- compose with `map`, `bind`, `tap`, and `orElse`
- collect lists and sequences

<div class="docs-grid">

<section class="docs-card">
<span class="label">Flow</span>
<h2>Sync boundaries</h2>
<p>The <code>Flow&lt;'env, 'error, 'value&gt;</code> type and <code>Flow</code> module cover synchronous composition, explicit environment reads, short-circuiting, and execution.</p>
<p>The next page expands the full member map, including creation helpers, composition helpers, environment access, traversals, and interop points.</p>
<div class="docs-card-links">
<a href="/reference/flow/">Open the Flow page</a>
</div>
</section>

<section class="docs-card">
<span class="label">Capability</span>
<h2>Capability boundaries</h2>
<p><code>FsFlow.Capability</code> keeps the named-capability story explicit while staying on the unified flow surface.</p>
<p>The <code>Capability</code> page covers the request tokens, record projections, service-provider lookups, and layer composition helpers.</p>
<div class="docs-card-links">
<a href="/reference/capability/">Open the Capability page</a>
</div>
</section>

<section class="docs-card">
<span class="label">Validation</span>
<h2>Accumulating validation</h2>
<p><code>FsFlow.Validation</code> gives an accumulating result for sibling failures and structured diagnostics.</p>
<p>The <code>Validation</code> page expands the full member map and the builder used to collect diagnostics into a graph.</p>
<div class="docs-card-links">
<a href="/reference/validation/">Open the Validation page</a>
</div>
</section>

<section class="docs-card">
<span class="label">Check</span>
<h2>Pure validation</h2>
<p><code>FsFlow.Check</code> gives pure <code>Result&lt;_, unit&gt;</code> checks for booleans, options, value options, nulls, collections, equality, and strings.</p>
<p>The <code>Check</code> page expands the full member map and the bridge that turns unit failures into application errors.</p>
<div class="docs-card-links">
<a href="/reference/check/">Open the Check page</a>
</div>
</section>

<section class="docs-card">
<span class="label">Diagnostics</span>
<h2>Validation graph</h2>
<p><code>FsFlow.Diagnostics</code> keeps validation failures structured so callers can flatten or render them after aggregation.</p>
<p>The <code>Diagnostics</code> page expands the graph shape and the associated merge and formatting helpers.</p>
<div class="docs-card-links">
<a href="/reference/diagnostics/">Open the Diagnostics page</a>
</div>
</section>

</div>

<div class="docs-stack">

<section class="docs-card">
<span class="label">CAPS packages</span>
<h3>Shared context packages</h3>
<p>The CAPS packages keep common application concerns explicit: <code>FsFlow.Caps.Core</code> for shared primitives and <code>FsFlow.Caps.Context</code> for request-scoped metadata.</p>
</section>

<section class="docs-card">
<span class="label">Entry points</span>
<h3>Workflow builders</h3>
<p>The <code>flow {}</code> and <code>validate {}</code> entry points are available, but the builder types themselves are treated as plumbing rather than headline API.</p>
<p>Use the builders for readable orchestration, and use the modules for the actual API surface.</p>
</section>

</div>

## Read the subpages

<div class="docs-grid">

<section class="docs-card">
<span class="label">Flow</span>
<h3><a href="/reference/flow/">Sync boundaries</a></h3>
<p>Type, module, composition, environment, and execution in the flow family.</p>
</section>

<section class="docs-card">
<span class="label">Capability</span>
<h3><a href="/reference/capability/">Named capability helpers</a></h3>
<p>Request tokens, service lookup, and environment/layer composition.</p>
</section>

<section class="docs-card">
<span class="label">Check</span>
<h3><a href="/reference/check/">Pure checks</a></h3>
<p>Pure predicates and the bridge to effectful error creation.</p>
</section>

<section class="docs-card">
<span class="label">Validation</span>
<h3><a href="/reference/validation/">Accumulating validation</a></h3>
<p>The <code>Validation</code> type and <code>validate {}</code> builder for collecting diagnostics into a structured graph.</p>
</section>

<section class="docs-card">
<span class="label">Diagnostics</span>
<h3><a href="/reference/diagnostics/">Structured diagnostics</a></h3>
<p>The validation graph and the helpers that render and flatten it.</p>
</section>

<section class="docs-card">
<span class="label">CAPS Core</span>
<h3><a href="/reference/caps-core/">Shared primitives</a></h3>
<p>The clock, random, GUID, and environment-variable package.</p>
</section>

<section class="docs-card">
<span class="label">CAPS Context</span>
<h3><a href="/reference/caps-context/">Request context</a></h3>
<p>Request-scoped user, locale, metadata, and flag helpers.</p>
</section>

</div>
