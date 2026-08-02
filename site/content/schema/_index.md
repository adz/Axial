---
title: "Schema: parse, don't validate"
linkTitle: Schema
description: Parse untrusted input through field constraints and domain constructors, or return path-aware diagnostics.
type: docs
notoc: true
weight: 8
menu:
  main:
    weight: 5
---

<div class="docs-home-container axial-landing">

<div class="docs-home-hero">

<div class="docs-home-copy">
<span class="eyebrow" style="color:#0b55d9">Axial &middot; Parse-don't-validate</span>

<h1>Parse, don't validate.</h1>

<div class="lede">
Validators start with an object that already exists. That leaves application code to track whether validation ran,
keep field paths aligned with checks, and repeat the same rules for parsing, forms, codecs, and contract documents.
Axial starts one step earlier: a <code>Schema</code> describes how untrusted boundary values become a model. If a field
or constructor invariant fails, parsing returns `SchemaErrors` and does not return the model.
</div>

<div class="lede">
The declaration is reusable data. Input parsing executes it; inspection, JSON Schema, codecs, versioned contracts,
and test-data generation interpret the same field names, value shapes, and constraints for their own jobs.
</div>

<div class="docs-home-meta">
<a class="docs-home-cta" href="{{< relref "/schema/getting-started.md" >}}">Get started &gt;</a>
<a class="docs-chip" href="{{< relref "/schema/getting-started.md" >}}">Getting started guide</a>
<a class="docs-chip" href="{{< relref "/schema/overview-examples.md" >}}">Overview examples</a>
<a class="docs-chip" href="{{< relref "/schema/reference-apps.md" >}}">Reference apps walkthrough</a>
</div>
</div>

<div class="docs-home-hero-visual">
<svg viewBox="0 0 680 300" role="img" xmlns="http://www.w3.org/2000/svg" style="max-width:100%;height:auto">
  <title>Axial.Schema: one declaration drives everything below</title>
  <desc>A single field declaration fans downward into JSON codecs, parsers, contracts, docs and forms, and tests.</desc>
  <g font-size="13">
    <text x="20" y="24" fill="currentColor" opacity="0.55">One declaration drives all of it</text>
    <rect x="240" y="50" width="200" height="100" rx="8" fill="none" stroke="#0b55d9" stroke-width="1.25"/>
    <g fill="#0b55d9" font-family="var(--font-mono, monospace)" font-size="12">
      <text x="258" y="78">type Order =</text>
      <text x="258" y="98">  id : OrderId</text>
      <text x="258" y="118">  items : Item list</text>
      <text x="258" y="138">  total : Money</text>
    </g>
    <g stroke="currentColor" stroke-opacity="0.35">
      <line x1="340" y1="150" x2="76" y2="216"/>
      <line x1="340" y1="150" x2="198" y2="216"/>
      <line x1="340" y1="150" x2="320" y2="216"/>
      <line x1="340" y1="150" x2="442" y2="216"/>
      <line x1="340" y1="150" x2="564" y2="216"/>
    </g>
    <g fill="none" stroke="currentColor" stroke-opacity="0.4">
      <rect x="20" y="216" width="112" height="30" rx="6"/>
      <rect x="142" y="216" width="112" height="30" rx="6"/>
      <rect x="264" y="216" width="112" height="30" rx="6"/>
      <rect x="386" y="216" width="112" height="30" rx="6"/>
      <rect x="508" y="216" width="112" height="30" rx="6"/>
    </g>
    <g fill="currentColor" text-anchor="middle" font-family="var(--font-mono, monospace)" font-size="11">
      <text x="76" y="235">JSON codecs</text>
      <text x="198" y="235">Parsers</text>
      <text x="320" y="235">Contracts</text>
      <text x="442" y="235">Docs, forms</text>
      <text x="564" y="235">Tests</text>
    </g>
  </g>
</svg>
</div>

</div>

<div style="max-width: 68ch;">

## Packages

The Schema documentation covers the core package and its focused codec, contract, HTTP, and testing packages.

| Package | Use it for | Documentation |
| --- | --- | --- |
| `Axial.Schema` | Model schemas, parsing, checking, accumulated errors, and inspection | [Axial.Schema](./overview/) |
| `Axial.Schema.Json` | Compiled JSON codecs | [JSON Codec](./json-codec/) |
| `Axial.Schema.JsonSchema` | JSON Schema generation | [JSON Schema reference]({{< relref "/schema/reference/schema/m-schema-jsonschema-generate" >}}) |
| `Axial.Schema.Contracts.Build` | Build-time schema derivation from F# records | [Derived Schemas](./derivation/) |
| `Axial.Schema.Http` | HTTP-neutral request and response contracts | [HTTP Servers](./http-servers/) |
| `Axial.Schema.Http.AspNetCore` | ASP.NET Core integration | [ASP.NET Core reference]({{< relref "/schema/reference/schema/http/aspnetcore" >}}) |
| `Axial.Schema.Http.GenHttp` | GenHTTP integration | [GenHTTP reference]({{< relref "/schema/reference/schema/http/genhttp" >}}) |
| `Axial.Schema.Testing` | Test helpers for schema guarantees | [Testing patterns](./patterns/testing-schema-guarantees/) |

Schema controls values produced through Schema. A public F# record can still be constructed directly. Use refined fields,
a private aggregate, or an opaque `.fsi` interface when the rest of the application must rely on an invariant
without checking it again — see [Trusted Construction](./trusted-construction/).

## Related Products

[Error Handling]({{< relref "/error-handling/" >}}) provides the reusable checks and refined values that Schema
uses. It can also be installed and used on its own. [Flow]({{< relref "/flow/" >}}) models effects and dependencies;
Schema does not require it.

See [Axial.Schema](./overview/) for the core package's mental model, installation, and full guide list.

</div>

</div>
