---
weight: 1
title: Axial.Schema
description: The core package's mental model, installation, and guides.
---

# Axial.Schema

[Result]({{< relref "/result/" >}}) and the [Values]({{< relref "/values/" >}}) packages supply operations over individual values and ordinary
`Result` composition. Axial.Schema assembles checks, constraints, parsing steps, and refinements into declarations for
whole structured models. It adds field identity, path-aware accumulated errors, checked reconstruction, and multiple
interpreters; it does not replace the underlying value-level APIs.

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
must satisfy an invariant, give the type a private representation and a checked `Refinement`;
[Trusted Construction](../trusted-construction/) shows how drafts keep record syntax and `with` updates alongside
that guarantee.

The declaration vocabulary covers primitive and refined values, nested models, lists, maps, optional values, three
tagged-union shapes, and recursive models. `Contract` keeps frozen wire versions and typed migrations outside the
current domain model.

## Guides

- [Getting Started](../getting-started/) — one model built up in four stages: plain fields, refined fields, field
  constraints, and a private model behind a checked constructor. **Start here.**
- [Schema Overview Examples](../overview-examples/) — short examples of inference, checked construction, refinement,
  recursion, and core interpreters.
- [Tutorials](../tutorials/) — parse a signup form, nest models, apply rules, and inspect metadata.
- [Schema Syntax](../syntax/) — constructor-last declarations and field blocks.
- [Derived Schemas](../derivation/) — generate declarations from F# records, configure MSBuild, and browse attributes.
- [Field Blocks and Plain Functions](../field-desugaring/) — how `withSchema`, `constrain`, `refine`, and `validate`
  correspond to ordinary schema transformations.
- [Input Sources](../input-sources/) — HTTP form-like, CLI, JSON-like, and configuration input.
- [Redisplay And Field Errors](../redisplay-and-field-errors/) — failed parses that keep the user's input.
- [Trusted Construction](../trusted-construction/) — checked public records, refined fields, and private aggregates.
- [Refined Value Schemas](../refined-values/) — domain values like `Email` as portable field schemas.
- [Union Schemas](../union-schemas/) — tagged discriminated unions as schema fields.
- [JSON Codec](../json-codec/) — compile the same declaration into a runtime-reflection-free JSON codec for trusted payloads.
- [HTTP Servers](../http-servers/) — schema-trusted requests, problem details, and generated OpenAPI.
- [Versioned Contracts](../contracts/) — evolve wire formats without freezing the domain model.
- [Recommended Patterns](../patterns/) — private aggregates, legal transitions, wire/domain separation, project layout,
  and the repository's test-adapter pattern.
- [Packages and Platforms](../packages-and-platforms/) — package boundaries and .NET/Fable JavaScript support.

## In Practice

- [Runnable Examples](../examples/) — executed during the docs build, mirrored back into the site.
- [Benchmarks](../benchmarks/) — measured parse and codec numbers on .NET and Fable.
- [Compiler-Directed, AOT, and Fable](../aot-trimming-fable/) — why the guarantees hold by construction.
- Comparisons: [vs zod](../comparisons/zod-comparison/), [vs FluentValidation](../comparisons/fluentvalidation-comparison/).

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

See [JSON Codec](../json-codec/) for what that package buys you.
