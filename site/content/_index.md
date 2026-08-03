---
title: Axial
description: Independent Data, Error Handling, Schema, and Flow libraries for F# on .NET and Fable JavaScript.
body_class: axial-home
---

<div class="docs-home-container axial-landing">

<div class="docs-home-hero">

<div class="docs-home-hero-visual">
<img class="hero-lockup hero-lockup--light" src="/content/img/hero-lockup-light.png" alt="Axial" width="1560" height="600" />
<img class="hero-lockup hero-lockup--dark" src="/content/img/hero-lockup-dark.png" alt="Axial" width="1560" height="600" />
</div>

<div class="docs-home-copy" style="max-width: 78ch; margin: 0 auto;">

<span class="eyebrow">F# libraries for .NET and Fable JavaScript</span>
<h1>Encode each invariant once. Enforce it across the project.</h1>

<script>
(function () {
  var taglines = [
    "If it compiles, the invariant already held.",
    "Types that prove it, not comments that promise it.",
    "Stop trusting that the check ran. Make the type carry it.",
    "Untrusted in, proven out.",
    "Dependencies and failures belong in the signature, not in your head.",
    "Make illegal states unrepresentable — for values, boundaries, and effects."
  ];
  var el = document.getElementById("axial-tagline");
  if (el) {
    el.textContent = taglines[Math.floor(Math.random() * taglines.length)];
  }
})();
</script>

<div class="lede">
<p>Axial's goal is to replace repetitive, error-prone code with APIs that are ergonomic for humans and predictable for LLMs, reducing the context both need to produce reliable software.</p>
</div>
<p>F# already provides strong foundations: discriminated unions and records for modelling, immutability by default, and explicit handling of missing values. We build on those foundations to enforce project-wide rules across values, boundaries, failures, dependencies, and concurrent work.</p>

<p id="axial-tagline" class="axial-tagline">If it compiles, the invariant already held.</p>
</div>

</div>

<div class="axial-doors">

<a class="axial-door axial-door--data" href="{{< relref "/data/" >}}">
<span class="axial-door-kicker">Structured values and fixtures</span>
<h2>Axial.Data</h2>
<p>Build, edit, compare, and match portable structured values without repetitive constructors or copied fixtures.</p>
<span class="axial-door-cta">Data documentation &rarr;</span>
</a>

<a class="axial-door axial-door--result" href="{{< relref "/error-handling/result/" >}}">
<span class="axial-door-kicker">Typed failures</span>
<h2>Axial.Result</h2>
<p>Compose operations that can fail over the standard F# Result type, with <code>result { }</code> for
fail-fast sequencing and accumulating builders for collecting every error at once.</p>
<span class="axial-door-cta">Result documentation &rarr;</span>
</a>

<a class="axial-door axial-door--values" href="{{< relref "/error-handling/" >}}">
<span class="axial-door-kicker">Admitting values</span>
<h2>Values</h2>
<p>Three independently installable packages — Constraint, Refined, and Parse. Reuse value checks, parse
serialized primitives, and construct refined values, so an invalid value can't reach your domain types in
the first place.</p>
<span class="axial-door-cta">Values documentation &rarr;</span>
</a>

<a class="axial-door axial-door--parse" href="{{< relref "/schema/" >}}">
<span class="axial-door-kicker">Input and domain values</span>
<h2>Axial.Schema</h2>
<p>Declare how structured input becomes a model once, and get JSON codecs, contracts, and validation from that single
definition, so parsers, docs, forms, and tests can't drift out of sync with each other.</p>
<span class="axial-door-cta">Schema documentation &rarr;</span>
</a>

<a class="axial-door axial-door--flow" href="{{< relref "/flow/" >}}">
<span class="axial-door-kicker">Effects and execution</span>
<h2>Axial.Flow</h2>
<p>Describe async work with its required environment and expected failure type in the signature, so missing
dependencies and unhandled failures show up at compile time instead of in production. 
</p>
<span class="axial-door-cta">Flow documentation &rarr;</span>
</a>

</div>

<div class="docs-home-meta" style="margin-bottom: 4rem;">
<a class="docs-chip" href="https://github.com/adz/Axial">GitHub</a>
</div>

</div>
