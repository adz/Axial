---
title: Reference
type: docs
notoc: true
weight: 100
---

API reference for the Schema area, grouped by package.

## Axial.Schema

- [`Schema`](./schema/) — the portable model declaration: builders, fields, constraints, and metadata inspection.
- [`Schema Interpreters`](./schema/interpreters/) — input parsing, model validation, and rules over a schema
  (namespace `Axial.Validation.Schema`).
- [`Refined`](./refined/) — parse and refine single values into types that carry their own proof (namespace
  `Axial.Refined`).

## Axial.Codec (optional)

- [`Codec`](./codec/) — compiled JSON codecs and JSON Schema generation from the same declaration.

Accumulating validation (`Validation`, `Diagnostics`) ships in `Axial.ErrorHandling`, not this package — see the
[Error Handling reference]({{< relref "/error-handling/reference/" >}}).
