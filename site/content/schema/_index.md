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

<div style="max-width: 68ch; padding-top: 3rem;">
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

<div style="max-width: 68ch;">

## Packages

The Schema documentation covers the core package and its focused input, codec, contract, HTTP, and testing packages.

| Package | Use it for | Documentation |
| --- | --- | --- |
| `Axial.Data` | Source-neutral structured input values | [Data](./data/) |
| `Axial.Schema` | Model schemas, parsing, checking, accumulated errors, and inspection | [Axial.Schema](./overview/) |
| `Axial.Schema.Json` | Compiled JSON codecs | [JSON Codec](./json-codec/) |
| `Axial.Schema.JsonSchema` | JSON Schema generation | [JSON Schema reference]({{< relref "/schema/reference/schema/m-schema-jsonschema-generate" >}}) |
| `Axial.Schema.Contracts.Build` | Build-time checks for versioned contracts | [Versioned Contracts](./contracts/) |
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
