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
or constructor invariant fails, parsing returns diagnostics and does not return the model.
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

## Mental Model

One schema declaration, several interpreters:

| Input | Interpreter | Result |
| --- | --- | --- |
| `Data` | `Schema.parse schema` | model or `Diagnostics` |
| draft or imported value | `Schema.check schema` | the same value or `Diagnostics` |
| trusted model | `ContextRules.apply rules` | accepted model or contextual `Diagnostics` |
| schema | `Inspect.model` | finite metadata without execution |
| schema | `Json.compile` | reusable compiled JSON codec |
| schema | `JsonSchema.generate` | JSON Schema document |
| versioned `Data` | `Contract.parse` | current model or `ContractError` |
| schema | `SchemaGen.raw` / `SchemaGen.model` | FsCheck generators |

`Schema.check` covers typed values that did not arrive as structured data: a draft assembled with an ordinary record
literal (named fields, any order, compiler-checked completeness), or an existing value from an import or database
mapper. It runs every field's constraints and refinements again and re-invokes the record constructor, so
cross-field invariants hold too. Success returns the value itself, not a proof wrapper — when every value of a type
must satisfy an invariant, give the type a private representation and a fallible constructor;
[Trusted Construction](./trusted-construction/) shows how drafts keep record syntax and `with` updates alongside
that guarantee.

The declaration vocabulary covers primitive and refined values, nested models, lists, maps, optional values, three
tagged-union shapes, and recursive models. `FieldRef` values name, read, and copy-update draft fields without repeating
wire-name strings. `Contract` keeps frozen wire versions and typed migrations outside the current domain model.

## Guides

- [Getting Started](./getting-started/) — declare a schema once and parse structured data into a trusted model.
- [Schema Overview Examples](./overview-examples/) — short, commented examples covering every Schema subsystem.
- [Tutorials](./tutorials/) — parse a signup form, nest models, apply rules, and inspect metadata.
- [Trusted Construction](./trusted-construction/) — ActiveModel ergonomics with F# trusted construction.
- [Recommended Patterns](./patterns/) — private aggregates, legal transitions, wire/domain separation, project layout,
  and schema-derived tests.
- [The Schema DSL](./dsl/) — open one module inside a schema definition and drop the qualified prefixes.
- [Choosing A Tool](./choosing-a-tool/) — Schema vs Input vs Rules, the three tools inside this package.
- [Refined Value Schemas](./refined-values/) — domain values like `Email` as portable field schemas.
- [Union Schemas](./union-schemas/) — tagged discriminated unions as schema fields.
- [Redisplay And Field Errors](./redisplay-and-field-errors/) — failed parses that keep the user's input.
- [Rules](./rules/) — contextual requirements over an already-trusted model.
- [JSON Codec](./json-codec/) — compile the same declaration into a runtime-reflection-free JSON codec for trusted payloads.
- [Input Sources](./input-sources/) — HTTP form-like, CLI, JSON-like, and configuration input.

## In Practice

- [Runnable Examples](./examples/) — executed during the docs build, mirrored back into the site.
- [Benchmarks](./benchmarks/) — measured parse and codec numbers on .NET and Fable.
- [Zero Reflection, AOT, and Fable](./aot-trimming-fable/) — why the guarantees hold by construction.
- Comparisons: [vs zod](./comparisons/zod-comparison/), [vs FluentValidation](./comparisons/fluentvalidation-comparison/),
  [Validus integration](./comparisons/validus-comparison/).

## The Machinery

Two subsections hold the tools schemas are built from — both ship in `Axial.ErrorHandling`, not this package;
Schema uses them, they aren't Schema-specific:

- [Refined]({{< relref "/error-handling/refined/" >}}) — single values whose types carry their own proof:
  `PositiveInt`, `NonBlankString`, your own.
- [Validation]({{< relref "/validation/" >}}) — accumulate every sibling failure as a path-aware diagnostics tree.

Axial consists of three packages: [Error Handling]({{< relref "/error-handling/" >}}) for pure fail-fast checks with
plain `Result`, Schema for domain models at data boundaries, and [Flow]({{< relref "/flow/" >}}) for the effects
around them.

## Install

Install the core package with `dotnet add package Axial.Schema`.

Schema metadata, input parsing, checking, and rules live in this one package; `Refined` and `Validation` arrive with
it as the `Axial.ErrorHandling` dependency — declaring a schema, parsing structured data, and inspecting metadata never
require a second install.

`Axial.Schema.Codec` is separate and optional: add it only if you want a compiled, runtime-reflection-free JSON codec generated from
your schema (`Json.compile`). Everything else — parsing, validation, rules, redisplay, JSON Schema generation — works
without it.

Install the optional codec with `dotnet add package Axial.Schema.Codec`.

See [JSON Codec](./json-codec/) for what that package buys you.

</div>

</div>
