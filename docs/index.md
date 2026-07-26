---
title: Axial
description: Independent Error Handling, Schema, and Flow libraries for F# on .NET and Fable JavaScript.
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

<p id="axial-tagline" class="axial-tagline">If it compiles, the invariant already held.</p>
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
<p>F# already provides strong foundations: discriminated unions and records for modelling, immutability by default, and explicit handling of missing values.</p>

<p>Axial builds on those foundations to enforce project-wide rules across values, boundaries, failures, dependencies, and concurrent work.</p>

<p>The goal is to replace repetitive, error-prone code with APIs that are ergonomic for humans and predictable for LLMs, reducing the context both need to produce reliable software.</p>
</div>
</div>

</div>

<div class="axial-doors">

<a class="axial-door axial-door--validation" href="{{< relref "/error-handling/" >}}">
<span class="axial-door-kicker">Checks and typed failures</span>
<h2>Axial.ErrorHandling</h2>
<p>Compose ordinary Results and reuse value checks with preset CheckError types.
Parse and construct refined values, so an invalid value can't reach your
domain types in the first place.</p>
<span class="axial-door-cta">Error Handling documentation &rarr;</span>
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
