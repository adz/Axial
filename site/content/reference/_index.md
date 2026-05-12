---
title: Reference
description: Module index for the public FsFlow API surface.
type: docs
weight: 30
---

# API Reference

This categorical index covers the workflow surface, the capability helpers, the pure validation bridge, and the CAPS support packages.

The FsFlow ecosystem groups the main public surfaces:

- `Flow<'env, 'error, 'value>` and the [`Flow`](/reference/flow/) module
- [`Capability`](/reference/capability/) for named capability helpers and environment access
- [`Check`](/reference/check/) for pure `Result<'value, unit>` predicates
- [`Validation`](/reference/validation/) for accumulated diagnostics
- [`Diagnostics`](/reference/diagnostics/) for the structured validation graph
- [`Ref`](/reference/ref/) for atomic mutable references
- [`Schedule`](/reference/schedule/) for retry and repeat policies
- [`STM`](/reference/stm/) for software transactional memory
- [`Stream`](/reference/stream/) for effectful pull-based streams
- the [`flow {}`](/reference/flow/builders-flow.md) and [`validate {}`](/reference/validation/builders-validate.md) entry points
- the CAPS request tokens [`Needs<'dep>`](/reference/caps/) and [`Env<'dep>`](/reference/caps/) / `Env<'dep, 'value>` for explicit capability boundaries
- the shared primitives [`FsFlow.Caps.Core`](/reference/caps-core/)
- the live capabilities [`FsFlow.Caps.Console`](/reference/caps-console/), [`FileSystem`](/reference/caps-filesystem/), [`Http`](/reference/caps-http/), and [`Process`](/reference/caps-process/)
- the host integration [`FsFlow.Hosting`](/reference/hosting/)
- the observability integration [`FsFlow.Runtime.Telemetry`](/reference/telemetry/)

The builder types themselves stay below the surface. The modules are the public story.

## Read the subpages

<div class="docs-grid">

<section class="docs-card">
<span class="label">Flow</span>
<h3><a href="/reference/flow/">Execution boundaries</a></h3>
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
<p>The <code>Validation</code> type and <code>validate {}</code> builder for collecting diagnostics.</p>
</section>

<section class="docs-card">
<span class="label">Diagnostics</span>
<h3><a href="/reference/diagnostics/">Structured diagnostics</a></h3>
<p>The validation graph and the helpers that render and flatten it.</p>
</section>

<section class="docs-card">
<span class="label">Ref</span>
<h3><a href="/reference/ref/">Atomic references</a></h3>
<p>Thread-safe mutable state handles for concurrent workflows.</p>
</section>

<section class="docs-card">
<span class="label">Schedule</span>
<h3><a href="/reference/schedule/">Execution policies</a></h3>
<p>DSL for describing retry and repetition strategies.</p>
</section>

<section class="docs-card">
<span class="label">STM</span>
<h3><a href="/reference/stm/">Transactional memory</a></h3>
<p>Composable atomic transactions across multiple state variables.</p>
</section>

<section class="docs-card">
<span class="label">Stream</span>
<h3><a href="/reference/stream/">Effectful streams</a></h3>
<p>Asynchronous, pull-based streams with environment and error support.</p>
</section>

<section class="docs-card">
<span class="label">CAPS Core</span>
<h3><a href="/reference/caps-core/">Shared primitives</a></h3>
<p>The clock, random, GUID, and environment-variable package.</p>
</section>

<section class="docs-card">
<span class="label">CAPS IO</span>
<h3><a href="/reference/caps-console/">System IO</a></h3>
<p>Console, FileSystem, HTTP, and Process capability packages.</p>
</section>

<section class="docs-card">
<span class="label">Hosting</span>
<h3><a href="/reference/hosting/">Host integration</a></h3>
<p>Microsoft.Extensions.DependencyInjection and startup validation.</p>
</section>

<section class="docs-card">
<span class="label">Telemetry</span>
<h3><a href="/reference/telemetry/">Observability</a></h3>
<p>OpenTelemetry and Activity.trace integration.</p>
</section>

</div>
