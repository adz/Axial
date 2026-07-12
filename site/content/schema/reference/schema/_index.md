---
title: "Schema"
weight: 10
type: docs
---

This page shows `Schema<'value>`, the universal catalog for primitive, collection, optional, union, refined, and record declarations. The same declaration can be parsed, checked, inspected, encoded, documented, and used for generation.

## Core types

- [`Schema`](./t-schema-schema.md):
 Describes a typed value&#39;s portable structure and construction for schema interpreters.

- [`Schema.Field`](./t-schema-field.md):
 Describes one typed field of a trusted model for schema interpreters.

- [`Schema.UnionCase`](./t-schema-unioncase.md): Describes one tagged union case for <code>Schema.union</code>.

## Catalog

- [`Schema.text`](./p-schema-schema-text.md): Describes text input.
- [`Schema.int`](./p-schema-schema-int.md): Describes a 32-bit integer.
- [`Schema.decimal`](./p-schema-schema-decimal.md): Describes a decimal number.
- [`Schema.bool`](./p-schema-schema-bool.md): Describes a Boolean value.
- [`Schema.dateTime`](./p-schema-schema-datetime.md): Describes a date and time with an offset.
- [`Schema.guid`](./p-schema-schema-guid.md): Describes a GUID.
- [`Schema.list`](./m-schema-schema-list.md): Describes a list whose items use <span class="fsdocs-param-name">item</span>.
- [`Schema.option`](./m-schema-schema-option.md): Describes an optional value.
- [`Schema.refine`](./m-schema-schema-refine.md): Maps a schema through a fallible smart constructor and lowers its failures to schema errors.
- [`Schema.union`](./m-schema-schema-union.md): Describes an externally tagged union.
- [`Schema.UnionCase.create`](./m-schema-unioncase-create.md):
 Describes one tagged union case from a tag, a payload constructor, a payload extractor, and a payload schema.


## Inspection

- [`Schema.SchemaShape`](./t-schema-schemashape.md):
 Describes the shape of a value schema as inspectable metadata for non-validation interpreters.

- [`Schema.SchemaDescription`](./t-schema-schemadescription.md): Describes one value schema: its shape, declared format, and portable constraint metadata.
- [`Schema.FieldDescription`](./t-schema-fielddescription.md): Describes one field of a model schema for inspection interpreters.
- [`Schema.ModelDescription`](./t-schema-modeldescription.md): Describes a built model schema as an ordered list of field descriptions.
- [`Schema.UnionDescription`](./t-schema-uniondescription.md): Describes a tagged union value schema.
- [`Schema.UnionCaseDescription`](./t-schema-unioncasedescription.md): Describes one case in a tagged union value schema.
- [`Schema.Inspect.model`](./m-schema-inspect-model.md): Describes a built model schema as inspectable field metadata.
- [`Schema.Inspect.schema`](./m-schema-inspect-schema.md): Describes a value schema as inspectable shape, format, and constraint metadata.
- [`Schema.Inspect.field`](./m-schema-inspect-field.md): Describes a standalone schema field as inspectable field metadata.

## JSON Schema generation

- [`Schema.JsonSchema.generate`](./m-schema-jsonschema-generate.md): Generates a compact JSON Schema document from a built model schema&#39;s metadata.
- [`Schema.JsonSchema.generateValue`](./m-schema-jsonschema-generatevalue.md): Generates a compact JSON Schema document for a standalone value schema.
