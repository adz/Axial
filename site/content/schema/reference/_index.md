---
title: Reference
type: docs
notoc: true
weight: 100
---

API reference for the Schema area, grouped by package and module.

## Axial.Schema

- [`Schema`](./schema/) — the portable model declaration: builders, fields, constraints, and metadata inspection.
- [`Schema Interpreters`](./schema/interpreters/) — the `Axial.Validation.Schema` module family:
  - [`RawInput`](./schema/interpreters/#raw-input) — source-agnostic raw input captured at a data boundary.
  - [`Input` / `ParsedInput`](./schema/interpreters/#input-parsing) — parsing boundary input through a schema.
  - [`SchemaError`](./schema/interpreters/#errors) — schema input, model validation, and rule failures.
  - [`RefinedSchema`](./schema/interpreters/#refined-catalog-schemas) — refined-value catalog schemas.
  - [`Validation`](./schema/interpreters/#model-validation) — intrinsic validation of an already-trusted model.
  - [`RuleSet` / `Rules`](./schema/interpreters/#rules) — contextual rules evaluated over an already-trusted model.
- [`Refined`](./refined/) — parse and refine single values into types that carry their own proof (namespace
  `Axial.Refined`).

## Axial.Codec

- [`Codec`](./codec/) — compiled JSON codecs and JSON Schema generation from the same declaration (namespace
  `Axial.Codec`). Optional: install it alongside `Axial.Schema` only when you need the compiled hot path.

Accumulating validation (`Validation`, `Diagnostics`) ships in `Axial.ErrorHandling`, not this package — see the
[Error Handling reference]({{< relref "/error-handling/reference/" >}}).
