---
title: "Error Handling: Result, checks, and refined values"
linkTitle: Error Handling
type: docs
notoc: true
description: Fail-fast results, reusable constraints, and domain values that record successful construction.
weight: 6
menu:
  main:
    weight: 4
---

<div class="docs-home-container axial-landing">

<div class="docs-home-hero">

<div class="docs-home-copy" style="max-width: 68ch;">
<span class="eyebrow" style="color:#0a7d62">Axial &middot; Result, Check, Refined</span>

<h1>Fail fast, reuse checks, carry the proof.</h1>

<div class="lede">
Most F# code already returns `Result<'value,'error>`. The gap is everything around it: rules you want to reuse
across several fields, and values whose type should record that a rule already passed. Axial splits that gap into
three small, independently installable packages instead of one large validation library.
</div>

<div class="lede">
None of the three requires the others. `Axial.Check` and `Axial.Refined` do not depend on `Axial.Result` — already
use FsToolkit.ErrorHandling, or your own `Result` helpers? Add Check and/or Refined on their own, with no builder or
module ambiguity.
</div>

<div class="docs-home-meta">
<a class="docs-home-cta" style="color:#0a7d62" href="{{< relref "/error-handling/overview/" >}}">Get started &gt;</a>
<a class="docs-chip" href="{{< relref "/error-handling/overview/" >}}">Result, Check, and Refined</a>
</div>
</div>

<div class="docs-home-hero-visual">
<svg viewBox="0 0 680 270" role="img" xmlns="http://www.w3.org/2000/svg" style="max-width:100%;height:auto">
  <title>Axial.ErrorHandling: each construct carries its own error type</title>
  <desc>Check defines CheckFailure, Parse defines ParseError, and Refined defines RefinementError. A thin Result module and computation expression sits underneath as the common surface.</desc>
  <g font-size="13">
    <text x="20" y="24" fill="currentColor" opacity="0.55">Each construct carries its own error type</text>
    <g stroke="currentColor" stroke-opacity="0.35" fill="none">
      <rect x="20" y="60" width="186" height="92" rx="8"/>
      <rect x="227" y="60" width="186" height="92" rx="8"/>
      <rect x="434" y="60" width="186" height="92" rx="8"/>
    </g>
    <g fill="currentColor" text-anchor="middle">
      <text x="113" y="84">Check</text>
      <text x="320" y="84">Parse</text>
      <text x="527" y="84">Refined</text>
    </g>
    <g fill="none" stroke="#0a7d62" stroke-width="1.25">
      <rect x="40" y="104" width="146" height="30" rx="4"/>
      <rect x="247" y="104" width="146" height="30" rx="4"/>
      <rect x="454" y="104" width="146" height="30" rx="4"/>
    </g>
    <g fill="#0a7d62" text-anchor="middle" font-family="var(--font-mono, monospace)" font-size="12">
      <text x="113" y="123">CheckFailure</text>
      <text x="320" y="123">ParseError</text>
      <text x="527" y="123">RefinementError</text>
    </g>
    <g stroke="currentColor" stroke-opacity="0.35">
      <line x1="113" y1="152" x2="113" y2="192"/>
      <line x1="320" y1="152" x2="320" y2="192"/>
      <line x1="527" y1="152" x2="527" y2="192"/>
    </g>
    <rect x="20" y="192" width="600" height="48" rx="8" fill="none" stroke="currentColor" stroke-opacity="0.5"/>
    <text x="320" y="212" text-anchor="middle" fill="currentColor">Result</text>
    <text x="320" y="230" text-anchor="middle" fill="currentColor" opacity="0.6" font-size="11">lightweight module and computation expression</text>
  </g>
</svg>
</div>

</div>

<div style="max-width: 68ch;">

## Packages

| Package | Use it for | Documentation |
| --- | --- | --- |
| `Axial.Result` | Fail-fast `Result` composition, conversions, extraction helpers, and `result { }` | [Result](./result/) |
| `Axial.Check` | Reusable, path-free constraints over one typed value, returning the standard F# `Result` | [Check](./check/) |
| `Axial.Refined` | Parsing and constructing values whose types record successful checks | [Refined](./refined/) |

[Axial.Schema]({{< relref "/schema/" >}}) builds on Check and Refined for structured, path-aware boundaries.
[Axial.Flow]({{< relref "/flow/" >}}) uses ordinary `Result` for typed workflow failures; neither depends on Result,
Check, or Refined.

See [Result, Check, and Refined](./overview/) for installation commands and a first look at each package, or go
straight to the one you need.

</div>

</div>
