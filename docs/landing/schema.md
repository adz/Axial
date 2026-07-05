---
title: "Schema: parse, don't validate"
linkTitle: Schema
description: Turn untrusted input into trusted domain models — invalid models are never constructed.
type: docs
notoc: true
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
raw input kept for redisplay, contextual rules, JSON codecs, and documentation.
</div>

<div class="docs-home-meta">
<a class="docs-home-cta" href="{{< relref "/schema/tutorials/" >}}">Get started &gt;</a>
<a class="docs-chip" href="{{< relref "/schema/getting-started.md" >}}">Getting started guide</a>
<a class="docs-chip" href="{{< relref "/schema/examples.md" >}}">Examples</a>
</div>
</div>

<div style="max-width: 68ch;">

## Mental Model

One schema declaration, several interpreters:

```text
RawInput -> Input.parse schema -> trusted model | Diagnostics
model    -> Validation.validate schema -> trusted model | Diagnostics
model    -> Rules.apply ruleSet -> trusted model | Diagnostics
schema   -> Inspect.model -> metadata (no execution)
schema   -> Json.compile -> compiled JSON codec (trusted hot path)
schema   -> JsonSchema.generate -> JSON Schema document
```

## Guides

- [Getting Started](./getting-started/) — declare a schema once and parse raw input into a trusted model.
- [Tutorials](./tutorials/) — parse a signup form, nest models, apply rules, and inspect metadata.
- [Trusted Construction](./trusted-construction/) — ActiveModel ergonomics with F# trusted construction.
- [Choosing A Tool](./choosing-a-tool/) — Schema vs Input vs Check vs Rules vs Policy.
- [Refined Value Schemas](./refined-values/) — domain values like `Email` as portable field schemas.
- [Union Schemas](./union-schemas/) — tagged discriminated unions as schema fields.
- [Redisplay And Field Errors](./redisplay-and-field-errors/) — failed parses that keep the user's input.
- [Rules And Policies](./rules-and-policies/) — contextual rules and environment-aware Flow policies.
- [JSON Codec](./json-codec/) — compile the same declaration into a reflection-free JSON codec for trusted payloads.
- [Input Sources](./input-sources/) — HTTP form-like, CLI, JSON-like, and configuration input.

## In Practice

- [Runnable Examples](./examples/) — executed during the docs build, mirrored back into the site.
- [Benchmarks](./benchmarks/) — measured parse and codec numbers on .NET and Fable.
- [Zero Reflection, AOT, and Fable](./aot-trimming-fable/) — why the guarantees hold by construction.
- Comparisons: [vs zod](./zod-comparison/), [vs FluentValidation](./fluentvalidation-comparison/),
  [Validus integration](./validus-comparison/).

## The Machinery

Two subsections hold the tools schemas are built from — reach for them directly when they pay for themselves:

- [Refined](./refined/) — single values whose types carry their own proof: `PositiveInt`, `NonBlankString`, your own.
- [Validation](./validation/) — accumulate every sibling failure as a path-aware diagnostics tree.

Axial consists of three parts: [Error Handling]({{< relref "/error-handling/" >}}) for pure fail-fast checks with
plain `Result`, Schema for domain models at data boundaries, and [Flow]({{< relref "/flow/" >}}) for the effects
around them.

## Package Layout

Core schema metadata lives in `Axial.Schema` and depends on nothing else. Interpreters that produce diagnostics —
input parsing, model validation, and rules — live in `Axial.Validation.Schema`. Policies that adapt those results into
workflows live in `Axial.Flow`.

</div>

</div>
