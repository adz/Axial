---
title: "Schema"
weight: 500
---

This page shows the core `Schema<'model>`, `ValueSchema<'value>`, and `Field<'model, 'value>` types. Schemas describe trusted model and value structure for interpreters such as input parsing, validation, codecs, JSON Schema, UI, and documentation. The core schema package stays independent of workflow execution, diagnostics, raw input, and validation interpreters.

## Core types

- [`Schema.Schema`](./t-schema-schema.md):
 Describes the portable structure of a trusted model for schema interpreters.

- [`Schema.ValueSchema`](./t-schema-valueschema.md):
 Describes the portable shape of a trusted value for schema interpreters.

- [`Schema.Field`](./t-schema-field.md):
 Describes one typed field of a trusted model for schema interpreters.
