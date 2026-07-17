---
title: Home
description: Axial — parse-don't-validate results and structured effects for F# on .NET.
---

<div class="docs-home-container axial-landing">

<div style="max-width: 68ch; padding-top: 3.5rem;">
<span class="eyebrow">F# on .NET &middot; zero reflection &middot; AOT &amp; Fable safe</span>

<h1>Structured data. Structured effects.</h1>

<div class="lede">
Axial consists of three packages that can be used independently but work together: plain <strong>Result</strong>
error handling for simple code, a <strong>parse-don't-validate</strong> toolkit that turns untrusted input into
trusted domain models, and a <strong>workflow model</strong> for the effects around them.
</div>
</div>

<div class="axial-doors">

<a class="axial-door axial-door--result" href="{{< relref "/error-handling/" >}}">
<span class="axial-door-kicker">Simple code?</span>
<h2>Error Handling</h2>
<p>Standard F# <code>Result</code> with your own error type is idiomatic Axial &mdash; <code>Check</code>, focused
helpers, and <code>result { }</code> remove the guard-clause boilerplate without changing your signatures.</p>
<span class="axial-door-cta">Enter &rarr;</span>
</a>

<a class="axial-door axial-door--parse" href="{{< relref "/schema/" >}}">
<span class="axial-door-kicker">Modelling a domain?</span>
<h2>Schema</h2>
<p>Describe how boundary input becomes a model. Failed input returns path-aware diagnostics; successful input reaches
the constructor only after its fields have parsed and passed their constraints.</p>
<span class="axial-door-cta">Enter &rarr;</span>
</a>

<a class="axial-door axial-door--flow" href="{{< relref "/flow/" >}}">
<span class="axial-door-kicker">Composing effects?</span>
<h2>Axial.Flow</h2>
<p>Describe async work with its dependencies and expected failure type. Tests supply small fake environments; hosts
supply live implementations and own cancellation and resource lifetime.</p>
<span class="axial-door-cta">Enter &rarr;</span>
</a>

</div>

<div class="docs-home-meta" style="margin-bottom: 4rem;">
<a class="docs-chip" href="{{< relref "/error-handling/getting-started.md" >}}">Start with Result</a>
<a class="docs-chip" href="{{< relref "/schema/getting-started.md" >}}">Start with Schema</a>
<a class="docs-chip" href="{{< relref "/flow/getting-started.md" >}}">Start with Flow</a>
<a class="docs-chip" href="https://github.com/adz/Axial">GitHub</a>
</div>

</div>
