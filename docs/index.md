---
title: Home
description: Axial — parse-don't-validate results and structured effects for F# on .NET.
---

<div class="docs-home-container axial-landing">

<div style="max-width: 68ch; padding-top: 3.5rem;">
<span class="eyebrow">F# on .NET &middot; zero reflection &middot; AOT &amp; Fable safe</span>

<h1>Structured data. Structured effects.</h1>

<div class="lede">
Axial is two tools that share one vocabulary. A <strong>parse-don't-validate</strong> toolkit that turns untrusted
input into trusted domain models &mdash; and a <strong>workflow model</strong> for the effects around them. Use either
alone; they meet exactly where a parsed model enters a workflow.
</div>
</div>

<div class="axial-doors">

<a class="axial-door axial-door--parse" href="{{< relref "/parse/" >}}">
<span class="axial-door-kicker">Modelling a domain?</span>
<h2>Parse, don't validate</h2>
<p>Declare the model once with <code>Schema</code>. Parsing, validation, redisplay, rules, and docs fall out &mdash;
an invalid model is never constructed. For simple code, plain <code>Result</code> with your own error type is the
whole story.</p>
<span class="axial-door-cta">Enter &rarr;</span>
</a>

<a class="axial-door axial-door--flow" href="{{< relref "/flow/" >}}">
<span class="axial-door-kicker">Composing effects?</span>
<h2>Axial.Flow</h2>
<p>A Reader-Async-Result workflow model in the ZIO tradition: explicit dependencies in <code>'env</code>, direct
<code>Task</code>/<code>Async</code> interop, cancellation, layers, fibers, and scheduling &mdash; without framework
lock-in.</p>
<span class="axial-door-cta">Enter &rarr;</span>
</a>

</div>

<div class="docs-home-meta" style="margin-bottom: 4rem;">
<a class="docs-chip" href="{{< relref "/docs/start/getting-started.md" >}}">Getting started</a>
<a class="docs-chip" href="{{< relref "/docs/" >}}">All guides</a>
<a class="docs-chip" href="{{< relref "/reference/" >}}">API reference</a>
<a class="docs-chip" href="https://github.com/adz/Axial">GitHub</a>
</div>

</div>
