---
title: "Schema"
weight: 500
type: docs
---

This page shows the core `Schema<'model>` and `ValueSchema<'value>` types. Schemas describe trusted model and value structure for interpreters such as input parsing, validation, codecs, JSON Schema, UI, and documentation. The core schema package stays independent of workflow execution, diagnostics, raw input, and validation interpreters.

## Core types

- [`Schema.Schema`](./t-schema-schema.md):
 Describes the portable structure of a trusted model for schema interpreters.

- [`Schema.ValueSchema`](./t-schema-valueschema.md):
 Describes the portable shape of a trusted value for schema interpreters.
