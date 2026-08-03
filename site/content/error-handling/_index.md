---
title: "Error Handling: Result, Constraint, Parse, and Refined"
linkTitle: Error Handling
type: docs
notoc: true
description: Compose typed failures, reusable checks, primitive parsing, and invariant-carrying values.
weight: 6
menu:
  main:
    weight: 4
---

<div class="docs-home-container axial-landing">

<div class="docs-home-hero">

<div class="docs-home-copy">
<span class="eyebrow" style="color:#0a7d62">Axial &middot; Result, Constraint, Parse, Refined</span>

<h1>Keep each failure at the operation that owns it.</h1>

<div class="lede">
Use ordinary Result composition, reusable value checks, named primitive parsers, and private domain types built from
reusable refinements. Each package can be installed independently.
</div>

<div class="docs-home-meta">
<a class="docs-home-cta" style="color:#0a7d62" href="{{< relref "/error-handling/overview/" >}}">Get started &gt;</a>
<a class="docs-chip" href="{{< relref "/error-handling/refined/domain-values/" >}}">Define a refined type</a>
</div>
</div>

<div class="docs-home-hero-visual">
<svg viewBox="0 0 680 285" role="img" xmlns="http://www.w3.org/2000/svg" style="max-width:100%;height:auto">
  <title>Result composes Constraint, Parse, and Refined operations</title>
  <desc>A constraint tests an existing value, Parse decodes a serialized primitive, and Refined guards construction and exposes the underlying Value. Constraint checking and refined construction return Violation values, while Parse returns ParseError. Result maps each failure into the application's error type.</desc>
  <g font-size="13">
    <text x="20" y="22" fill="currentColor" opacity="0.55">Explicit value operations, composed with Result</text>
    <g stroke="currentColor" stroke-opacity="0.35" fill="none">
      <rect x="20" y="42" width="186" height="136" rx="8"/>
      <rect x="227" y="42" width="186" height="136" rx="8"/>
      <rect x="434" y="42" width="186" height="136" rx="8"/>
    </g>
    <g fill="currentColor" text-anchor="middle">
      <text x="113" y="66">Constraint</text>
      <text x="320" y="66">Parse</text>
      <text x="527" y="66">Refined</text>
    </g>
    <g fill="currentColor" text-anchor="middle" opacity="0.62" font-size="11">
      <text x="113" y="84">test an existing value</text>
      <text x="320" y="84">decode serialized input</text>
      <text x="527" y="84">carry an invariant · operate on it</text>
    </g>
    <g fill="none" stroke="#0a7d62" stroke-width="1.25">
      <rect x="40" y="100" width="146" height="30" rx="4"/>
      <rect x="247" y="100" width="146" height="30" rx="4"/>
      <rect x="454" y="100" width="146" height="30" rx="4"/>
    </g>
    <g fill="#0a7d62" text-anchor="middle" font-family="var(--font-mono, monospace)" font-size="12">
      <text x="113" y="119">Violation</text>
      <text x="320" y="119">ParseError</text>
      <text x="527" y="119">Violation</text>
    </g>
    <g fill="currentColor" text-anchor="middle" opacity="0.55" font-size="10.5">
      <text x="113" y="151">Constraint adds portable metadata</text>
      <text x="527" y="151">Refinement owns constraints</text>
    </g>
    <g stroke="currentColor" stroke-opacity="0.35">
      <line x1="113" y1="178" x2="113" y2="210"/>
      <line x1="320" y1="178" x2="320" y2="210"/>
      <line x1="527" y1="178" x2="527" y2="210"/>
    </g>
    <rect x="20" y="210" width="600" height="50" rx="8" fill="none" stroke="currentColor" stroke-opacity="0.5"/>
    <text x="320" y="231" text-anchor="middle" fill="currentColor">Application Result</text>
    <text x="320" y="249" text-anchor="middle" fill="currentColor" opacity="0.6" font-size="11">map each source failure into the application's error type</text>
  </g>
</svg>
</div>

</div>

<div style="max-width: 68ch;">

## Packages

| Package | Use it for | Documentation |
| --- | --- | --- |
| `Axial.Result` | Result combinators, extraction helpers, and `result { }` | [Result](./result/) |
| `Axial.Constraint` | One reusable, inspectable description of valid values | [Constraint](./constraint/) |
| `Axial.Parse` | Serialized primitive decoding | [Parse](/error-handling/parse/) |
| `Axial.Refined` | Invariant-carrying domain values | [Refined](./refined/) |

`Axial.ErrorHandling` installs all four packages and exposes no API of its own.

[Axial.Schema]({{< relref "/schema/" >}}) adds structured input, paths, accumulated diagnostics, reconstruction, and
wire metadata.

</div>

</div>
