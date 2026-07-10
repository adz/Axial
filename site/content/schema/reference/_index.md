---
title: Reference
type: docs
notoc: true
weight: 100
---

API reference for the Schema area, grouped by package and module.

## Axial.Schema

- [`Schema`](./schema/) — the portable model declaration: builders, fields, constraints, and metadata inspection.
- [`Refined`](./refined/) — parse and refine single values into types that carry their own proof (namespace
  `Axial.Refined`).

Everything above and below shares the single `Axial.Schema` namespace (with `Axial.Refined` and `Axial.Codec`
carved out separately, see below) — the module name, not the namespace, is what separates declaration from
interpreter.

## Interpreters

Schemas are consumed by interpreters that stay independent of workflow execution and diagnostics:

- [`Schema Interpreters`](./schema/interpreters/) — the boundary-parsing and rule-evaluation surface:
  - [`RawInput`](./schema/interpreters/#raw-input) — source-agnostic raw input captured at a data boundary.
  - [`Input` / `ParsedInput`](./schema/interpreters/#input-parsing) — parsing boundary input through a schema.
  - [`SchemaError`](./schema/interpreters/#errors) — schema input, model validation, and rule failures.
  - [`RefinedSchema`](./schema/interpreters/#refined-catalog-schemas) — refined-value catalog schemas.
  - [`Validation`](./schema/interpreters/#model-validation) — intrinsic validation of an already-trusted model.
  - [`RuleSet` / `Rules`](./schema/interpreters/#rules) — contextual rules evaluated over an already-trusted model.
- [`JsonSchema`](./schema/#json-schema-generation) — generates a JSON Schema document from a built schema's
  metadata (still `Axial.Schema`, no extra package).

## Axial.Codec

- [`Codec`](./codec/) — compiled JSON codecs from the same declaration (namespace `Axial.Codec`). Optional:
  install it alongside `Axial.Schema` only when you need the compiled hot path.

Accumulating validation (`Validation`, `Diagnostics`) ships in `Axial.ErrorHandling`, not this package — see the
[Error Handling reference]({{< relref "/error-handling/reference/" >}}).
