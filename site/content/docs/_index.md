---
title: "Docs"
linkTitle: "Docs"
type: docs
weight: 20
---

Welcome to the Axial guides. Axial has two doors — [Parse, don't validate]({{< relref "/parse/" >}}) for domain models and data
boundaries, and [Flow]({{< relref "/flow/" >}}) for the effects around them. Start with
[Getting Started](./start/getting-started/).

<div class="docs-grid docs-index-grid">

<section class="docs-card">
<span class="label">Getting oriented</span>
<h2><a href="./start/">Start</a></h2>
<p>Install the package, learn the two-lane rule, and run small examples.</p>
</section>

<section class="docs-card">
<span class="label">Door one &middot; domain models</span>
<h2><a href="{{< relref "/parse/schema/" >}}">Schema</a></h2>
<p>Declare the model once: input parsing, validation, redisplay, contextual rules, policies, and metadata interpreters fall out.</p>
</section>

<section class="docs-card">
<span class="label">Door two &middot; simple code</span>
<h2><a href="{{< relref "/parse/error-handling/" >}}">Error Handling</a></h2>
<p>Plain F# Result with your own error type, kept terse by Check, focused helpers, and result { }.</p>
</section>

<section class="docs-card">
<span class="label">Effects</span>
<h2><a href="{{< relref "/flow/" >}}">Flow</a></h2>
<p>Environment access, async or task work, layers, resources, scheduling, concurrency, and service tutorials.</p>
</section>

<section class="docs-card">
<span class="label">Machinery &middot; single values</span>
<h2><a href="{{< relref "/parse/refined/" >}}">Refined</a></h2>
<p>Parse and refine individual boundary values; the toolkit schemas use for their fields.</p>
</section>

<section class="docs-card">
<span class="label">Machinery &middot; diagnostics</span>
<h2><a href="{{< relref "/parse/validation/" >}}">Validation</a></h2>
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

