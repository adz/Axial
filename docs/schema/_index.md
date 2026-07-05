---
weight: 28
title: Schema
type: docs
description: Portable model schemas for input parsing, validation, and metadata interpreters.
---

# Schema

This page shows how `Schema<'model>` drives parsing, validation, redisplay, rules, and metadata from one trusted model
declaration.

Use this section for `Schema<'model>`: a portable description of a trusted model's shape, field ordering, construction,
and constraint metadata.

A schema is not only validation. One schema drives several interpreters:

- **input parsing** turns raw boundary input into a trusted model or path-aware diagnostics
- **validation** re-checks an existing model through the same constraints
- **contextual rules** decide whether a trusted model is acceptable for one workflow
- **metadata interpreters** read the same schema for JSON Schema, documentation, and UI metadata without running any
  validation

## Mental Model

```text
RawInput -> Input.parse schema -> trusted model | Diagnostics
model    -> Validation.validate schema -> trusted model | Diagnostics
model    -> Rules.apply ruleSet -> trusted model | Diagnostics
schema   -> Inspect.model -> metadata (no execution)
```

At data boundaries, newcomer-facing failures use one shape: `SchemaError` inside path-aware `Diagnostics`. Primitive
`ParseError`, refined construction `RefinementError`, and path-free `CheckFailure` values lower into `SchemaError`;
`SchemaError.render` and `ParsedInput.renderErrors` provide default English display strings, and
`ParsedInput.mapErrors` maps that boundary shape into your own domain error union.

Schemas are authored with the progressive typed builder:

```fsharp
let contactSchema =
    Schema.recordFor<Contact, _> (fun name email -> { Name = name; Email = email })
    |> Schema.text "name" _.Name
    |> Schema.field "email" _.Email Email.schema
    |> Schema.build
```

Each field application peels one curried constructor argument, so constructor/getter alignment is compiler-checked and
authoring scales to any field count without a `mapN` family or code generation.

## Start Here

- [Tutorials](./tutorials/): parse a signup form, nest models, apply rules, and inspect metadata.
- [Trusted Construction](./trusted-construction/): ActiveModel ergonomics with F# trusted construction.
- [Choosing A Tool](./choosing-a-tool/): Schema vs Input vs Check vs Rules vs Policy.
- [Refined Value Schemas](./refined-values/): domain values like `Email` as portable field schemas.
- [Union Schemas](./union-schemas/): tagged discriminated unions as schema fields.
- [Domain Values](../refined/domain-values/): author caller-owned refined values in one module.
- [Redisplay And Field Errors](./redisplay-and-field-errors/): failed parses that keep the user's input.
- [Rules And Policies](./rules-and-policies/): contextual rules and environment-aware Flow policies.
- [Input Sources](./input-sources/): HTTP form-like, CLI, JSON-like, and configuration input.

## Package Layout

Core schema metadata lives in `Axial.Schema` and depends on nothing else. Interpreters that produce diagnostics — input
parsing, model validation, and rules — live in `Axial.Validation.Schema`. Policies that adapt those results into
workflows live in `Axial.Flow`.

## Reference

- [Schema API]({{< relref "/reference/schema/" >}})
