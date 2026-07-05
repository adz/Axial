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


## Value schemas

- [`Schema.Value.text`](./p-schema-value-text.md): Describes text represented as <a href="https://learn.microsoft.com/dotnet/api/system.string">String</a>.
- [`Schema.Value.int`](./p-schema-value-int.md): Describes a 32-bit signed integer represented as <a href="https://learn.microsoft.com/dotnet/api/system.int32">Int32</a>.
- [`Schema.Value.decimal`](./p-schema-value-decimal.md): Describes a decimal number represented as <a href="https://learn.microsoft.com/dotnet/api/system.decimal">Decimal</a>.
- [`Schema.Value.bool`](./p-schema-value-bool.md): Describes a Boolean value represented as <a href="https://learn.microsoft.com/dotnet/api/system.boolean">Boolean</a>.
- [`Schema.Value.dateTime`](./p-schema-value-datetime.md): Describes an instant-like date and time represented as <a href="https://learn.microsoft.com/dotnet/api/system.datetimeoffset">DateTimeOffset</a>.
- [`Schema.Value.guid`](./p-schema-value-guid.md): Describes a globally unique identifier represented as <a href="https://learn.microsoft.com/dotnet/api/system.guid">Guid</a>.
- [`Schema.Value.manyOf`](./m-schema-value-manyof.md): Describes a collection of values from an already built item value schema.

## Inspection

- [`Schema.ValueShape`](./t-schema-valueshape.md):
 Describes the shape of a value schema as inspectable metadata for non-validation interpreters.

- [`Schema.ValueDescription`](./t-schema-valuedescription.md): Describes one value schema: its shape, declared format, and portable constraint metadata.
- [`Schema.FieldDescription`](./t-schema-fielddescription.md): Describes one field of a model schema for inspection interpreters.
- [`Schema.ModelDescription`](./t-schema-modeldescription.md): Describes a built model schema as an ordered list of field descriptions.
- [`Schema.Inspect.model`](./m-schema-inspect-model.md): Describes a built model schema as inspectable field metadata.
- [`Schema.Inspect.value`](./m-schema-inspect-value.md): Describes a value schema as inspectable shape, format, and constraint metadata.
- [`Schema.Inspect.field`](./m-schema-inspect-field.md): Describes a standalone schema field as inspectable field metadata.
