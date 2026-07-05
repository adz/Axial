---
title: "Docs"
linkTitle: "Docs"
type: docs
weight: 20
---

Welcome to the Axial guides. Axial consists of three areas that can be used independently but work together:
[Error Handling]({{< relref "/error-handling/" >}}) for pure fail-fast checks,
[Schema]({{< relref "/schema/" >}}) for domain models at data boundaries, and
[Flow]({{< relref "/flow/" >}}) for the effects around them. Start with
[Getting Started](./start/getting-started/).

<div class="docs-grid docs-index-grid">

<section class="docs-card">
<span class="label">Getting oriented</span>
<h2><a href="./start/">Start</a></h2>
<p>Install the package, pick the right area for the work in front of you, and run small examples.</p>
</section>

<section class="docs-card">
<span class="label">Simple code</span>
<h2><a href="{{< relref "/error-handling/" >}}">Error Handling</a></h2>
<p>Plain F# Result with your own error type, kept terse by Check, focused helpers, and result { }.</p>
</section>

<section class="docs-card">
<span class="label">Domain models</span>
<h2><a href="{{< relref "/schema/" >}}">Schema</a></h2>
<p>Declare the model once: input parsing, validation, redisplay, contextual rules, policies, and metadata interpreters fall out.</p>
</section>

<section class="docs-card">
<span class="label">Effects</span>
<h2><a href="{{< relref "/flow/" >}}">Flow</a></h2>
<p>Environment access, async or task work, layers, resources, scheduling, concurrency, and service tutorials.</p>
</section>

<section class="docs-card">
<span class="label">Schema machinery &middot; single values</span>
<h2><a href="{{< relref "/schema/refined/" >}}">Refined</a></h2>
<p>Parse and refine individual boundary values; the toolkit schemas use for their fields.</p>
</section>

<section class="docs-card">
<span class="label">Schema machinery &middot; diagnostics</span>
<h2><a href="{{< relref "/schema/validation/" >}}">Validation</a></h2>
<p>Accumulating sibling failures with Validation, Diagnostics, and validate { } — the error trees schema parsing produces.</p>
</section>

<section class="docs-card">
<span class="label">Usage patterns</span>
<h2><a href="./patterns/">Patterns</a></h2>
<p>Use runnable examples, benchmarks, and type troubleshooting notes while applying Axial.</p>
</section>

<section class="docs-card">
<span class="label">Comparisons and integrations</span>
<h2><a href="./ecosystem/">Comparisons</a></h2>
<p>Compare Axial with Validus, FsToolkit.ErrorHandling, FSharpPlus, and Effect-TS, and see where they fit together.</p>
</section>

</div>

