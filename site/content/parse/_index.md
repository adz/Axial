---
title: Parse, don't validate
linkTitle: Parse
description: Turn untrusted input into trusted domain models — invalid models are never constructed.
weight: 8
menu:
  main:
    weight: 5
---

<div class="docs-home-container axial-landing">

<div style="max-width: 68ch; padding-top: 3rem;">
<span class="eyebrow" style="color:#0b55d9">Axial &middot; Parse-don't-validate</span>

<h1>Parse, don't validate.</h1>

<div class="lede">
Validators check objects that already exist &mdash; so the invalid object gets constructed first, and every code path
after it has to trust that someone, somewhere, ran the checks. Axial inverts this: declare the model once as a
<code>Schema</code>, and parsing goes <em>through</em> the declaration. If a constraint fails, the model is never
constructed. Holding the value is the proof it was valid.
</div>

<div class="lede">
One declaration drives everything that usually drifts apart: input parsing, re-validation, path-aware errors with the
raw input kept for redisplay, contextual rules, JSON codecs, and documentation. And when there is no domain model to
protect, plain F# <code>Result</code> with your own error type is the whole story &mdash; idiomatic Axial, not a
compromise.
</div>

<div class="docs-home-meta">
<a class="docs-home-cta" href="{{< relref "/parse/schema/tutorials/" >}}">Get started &gt;</a>
<a class="docs-chip" href="{{< relref "/docs/start/getting-started.md" >}}">Getting started guide</a>
<a class="docs-chip" href="{{< relref "/docs/patterns/examples/" >}}">Examples</a>
</div>
</div>

<div style="max-width: 68ch;">

## In this section

- [Schema](./schema/) — declare the model once; parsing, validation, redisplay, rules, and metadata fall out.
- [Error Handling](./error-handling/) — plain `Result` with your own error type, kept terse by `Check` and `result {}`.
- [Refined](./refined/) — single values whose types carry their own proof: `PositiveInt`, `NonBlankString`, your own.
- [Validation](./validation/) — accumulate every sibling failure as a path-aware diagnostics tree.

Everything here is independent of [Flow](../flow/) — parse at the boundary, then hand a trusted model to whatever
runs your application.

</div>

</div>
