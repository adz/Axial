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

<div class="lede">
Schema controls values produced through Schema. A public F# record can still be constructed directly. Use refined fields,
a private aggregate, or an opaque <code>.fsi</code> interface when the rest of the application must rely on an invariant
without checking it again.
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
| `Axial.Schema` | Model schemas, parsing, checking, accumulated errors, and inspection | [Getting Started](./getting-started/) |
| `Axial.Schema.Json` | Compiled JSON codecs | [JSON Codec](./json-codec/) |
| `Axial.Schema.JsonSchema` | JSON Schema generation | [JSON Schema reference]({{< relref "/schema/reference/schema/m-schema-jsonschema-generate" >}}) |
| `Axial.Schema.Contracts.Build` | Build-time checks for versioned contracts | [Versioned Contracts](./contracts/) |
| `Axial.Schema.Http` | HTTP-neutral request and response contracts | [HTTP Servers](./http-servers/) |
| `Axial.Schema.Http.AspNetCore` | ASP.NET Core integration | [ASP.NET Core reference]({{< relref "/schema/reference/schema/http/aspnetcore" >}}) |
| `Axial.Schema.Http.GenHttp` | GenHTTP integration | [GenHTTP reference]({{< relref "/schema/reference/schema/http/genhttp" >}}) |
| `Axial.Schema.Testing` | Test helpers for schema guarantees | [Testing patterns](./patterns/testing-schema-guarantees/) |

## Mental Model

One schema declaration, several interpreters:

| Input | Interpreter | Result |
| --- | --- | --- |
| `Data` | `Schema.parse schema` | model or `SchemaErrors` |
| draft or imported value | `Schema.check schema` | the same value or `SchemaErrors` |
| schema | `Inspect.model` | finite metadata without execution |
| schema | `Json.compile` | reusable compiled JSON codec |
| schema | `JsonSchema.generate` | JSON Schema document |
| versioned `Data` | `Contract.parse` | current model or `ContractError` |
| schema | repository-only `SchemaGen.raw` / `SchemaGen.model` adapter | FsCheck generators |

`Schema.check` covers typed values that did not arrive as structured data: a draft assembled with an ordinary record
literal (named fields, any order, compiler-checked completeness), or an existing value from an import or database
mapper. It runs every field's constraints and refinements again and re-invokes the record constructor, so
cross-field invariants hold too. Success returns the value itself, not a proof wrapper — when every value of a type
must satisfy an invariant, give the type a private representation and a fallible constructor;
[Trusted Construction](./trusted-construction/) shows how drafts keep record syntax and `with` updates alongside
that guarantee.

The declaration vocabulary covers primitive and refined values, nested models, lists, maps, optional values, three
tagged-union shapes, and recursive models. `Contract` keeps frozen wire versions and typed migrations outside the
current domain model.

## Guides

- [Getting Started](./getting-started/) — declare a schema once and parse structured data into a trusted model.
- [Schema Overview Examples](./overview-examples/) — short examples of inference, checked construction, refinement,
  recursion, and core interpreters.
- [Tutorials](./tutorials/) — parse a signup form, nest models, apply rules, and inspect metadata.
- [Schema Syntax](./syntax/) — constructor-last declarations and field blocks.
- [Field Blocks and Plain Functions](./field-desugaring/) — how `withSchema`, `constrain`, `refine`, and `validate`
  correspond to ordinary schema transformations.
- [Input Sources](./input-sources/) — HTTP form-like, CLI, JSON-like, and configuration input.
- [Redisplay And Field Errors](./redisplay-and-field-errors/) — failed parses that keep the user's input.
- [Trusted Construction](./trusted-construction/) — checked public records, refined fields, and private aggregates.
- [Refined Value Schemas](./refined-values/) — domain values like `Email` as portable field schemas.
- [Union Schemas](./union-schemas/) — tagged discriminated unions as schema fields.
- [JSON Codec](./json-codec/) — compile the same declaration into a runtime-reflection-free JSON codec for trusted payloads.
- [HTTP Servers](./http-servers/) — schema-trusted requests, problem details, and generated OpenAPI.
- [Versioned Contracts](./contracts/) — evolve wire formats without freezing the domain model.
- [Recommended Patterns](./patterns/) — private aggregates, legal transitions, wire/domain separation, project layout,
  and the repository's test-adapter pattern.
- [Packages and Platforms](./packages-and-platforms/) — package boundaries and .NET/Fable JavaScript support.

## In Practice

- [Runnable Examples](./examples/) — executed during the docs build, mirrored back into the site.
- [Benchmarks](./benchmarks/) — measured parse and codec numbers on .NET and Fable.
- [Compiler-Directed, AOT, and Fable](./aot-trimming-fable/) — why the guarantees hold by construction.
- Comparisons: [vs zod](./comparisons/zod-comparison/), [vs FluentValidation](./comparisons/fluentvalidation-comparison/).

## Related Products

[Error Handling]({{< relref "/error-handling/" >}}) provides the reusable checks and refined values that Schema
uses. It can also be installed and used on its own. [Flow]({{< relref "/flow/" >}}) models effects and dependencies;
Schema does not require it.

## Installation

Schema installs as part of `Axial`.

Or install it individually with `dotnet add package Axial.Schema`.

Schema metadata, input parsing, checking, accumulated errors, and executable validation live in this package. Checks
and refined values arrive through its focused package dependencies, so Schema users do not need a second install.

`Axial.Schema.Json` is separate and optional: add it only if you want a compiled, runtime-reflection-free JSON codec generated from
your schema (`Json.compile`). `Axial.Schema.JsonSchema` is also separate and optional; it supplies
`JsonSchema.generate` in the `Axial.Schema` namespace. Parsing, checking, rules, redisplay, and metadata inspection need
neither optional package.

`Axial.Schema.Json` also installs as part of `Axial`, or individually with `dotnet add package Axial.Schema.Json`.

Install JSON Schema generation with `dotnet add package Axial.Schema.JsonSchema`.

See [JSON Codec](./json-codec/) for what that package buys you.

</div>

</div>
