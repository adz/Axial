---
title: "Schema"
weight: 500
type: docs
---

This page shows the core `Schema<'model>`, `ValueSchema<'value>`, and `Field<'model, 'value>` types. Schemas describe trusted model and value structure for interpreters such as input parsing, validation, codecs, JSON Schema, UI, and documentation. The core schema package stays independent of workflow execution, diagnostics, raw input, and validation interpreters.

## Core types

- [`Schema.Schema`](./t-schema-schema.md):
 Describes the portable structure of a trusted model for schema interpreters.

- [`Schema.ValueSchema`](./t-schema-valueschema.md):
 Describes the portable shape of a trusted value for schema interpreters.

- [`Schema.Field`](./t-schema-field.md):
 Describes one typed field of a trusted model for schema interpreters.


## Inspection

- [`Schema.ValueShape`](./t-schema-valueshape.md):
 Describes the shape of a value schema as inspectable metadata for non-validation interpreters.

- [`Schema.ValueDescription`](./t-schema-valuedescription.md): Describes one value schema: its shape, declared format, and portable constraint metadata.
- [`Schema.FieldDescription`](./t-schema-fielddescription.md): Describes one field of a model schema for inspection interpreters.
- [`Schema.ModelDescription`](./t-schema-modeldescription.md): Describes a built model schema as an ordered list of field descriptions.
- [`Schema.Inspect.model`](./m-schema-inspect-model.md): Describes a built model schema as inspectable field metadata.
- [`Schema.Inspect.value`](./m-schema-inspect-value.md): Describes a value schema as inspectable shape, format, and constraint metadata.
- [`Schema.Inspect.field`](./m-schema-inspect-field.md): Describes a standalone schema field as inspectable field metadata.
