---
weight: 20
title: Constraint
type: docs
notoc: true
description: One reusable description of valid values, shared by checking, refined values, schema, and export.
---

# Constraint

A `Constraint<'value>` is a reusable description of valid values. **check** is the operation that runs it.

```fsharp
open Axial.Constraint

let retryCount : Constraint<int> =
    Constraint.between 0 10

3  |> Constraint.test retryCount   // true
42 |> Constraint.check retryCount  // Error (why 42 failed)
3  |> Constraint.guard retryCount  // Ok 3
```

There is one such vocabulary, not several. The same value works unchanged in a refinement and in a schema:

```fsharp
let retryCountRefinement =
    Refinement.define retryCount RetryCount _.Value

let schema =
    Schema.int |> Schema.constrain retryCount
```

That matters because everything downstream reads the same declaration. A constraint built from named parts lowers to
JSON Schema, generates test data, and renders localizable messages; the equivalent hand-written lambda does none of
those things, and says so honestly rather than pretending.

The catalogue resolves across text, collections, options, and maps by the type it is used at, so most uses need
nothing extra:

```fsharp
Schema.text |> Schema.constrain Constraint.present
```

A standalone binding is the exception: the annotation is the only type information there, so it is what selects the
shape.

```fsharp
let requiredName : Constraint<string> = Constraint.present
```

The same value facts appear at three further levels:

- [Refined values](../refined/domain-values/) use a constraint to construct invariant-carrying domain types.
- [Schema]({{< relref "/schema/refined-values/" >}}) adds structured input, paths, accumulation, and wire interpreters.
- JSON Schema publishes what the target can enforce, and documents the rest.

Start with the [Constraint DSL](./constraint-dsl/) for the vocabulary and how to write a rule module, then
[Using constraints](./overview/) for composition and violations, [Interpreted and opaque](./constraints/) for what
makes a rule inspectable and what an escape hatch costs, and [Localization](./localization/) for translating
failures.
