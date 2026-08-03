---
title: "Schema"
weight: 500
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
- [`Schema.list`](./m-schema-schema-list.md): Describes a list by resolving its item schema from <typeparamref name="'item" />.
- [`Schema.option`](./m-schema-schema-option.md): Describes an optional value.
- [`Schema.constrain`](./m-schema-schema-constrain.md): Requires a schema&#39;s values to satisfy a constraint.
- [`Schema.constrainAll`](./m-schema-schema-constrainall.md): Requires a schema&#39;s values to satisfy every constraint, in declaration order.
- [`Schema.mustSupply`](./m-schema-schema-mustsupply.md): Requires boundary input for this schema to be supplied.
- [`Schema.mayOmit`](./m-schema-schema-mayomit.md): Allows boundary input for an option-typed schema to be omitted.
- [`Schema.refine`](./m-schema-schema-refine.md): Maps a raw schema through a reusable bidirectional refinement.
- [`Schema.validate`](./m-schema-schema-validate.md): Adds executable value validation to a schema.
- [`Schema.union`](./m-schema-schema-union.md): Describes an externally tagged union.
- [`Schema.UnionCase.create`](./m-schema-unioncase-create.md):
 Describes one tagged union case from a tag, a payload constructor, a payload extractor, and a payload schema.

- [`Schema.Supply`](./t-schema-supply.md): Whether boundary input for a field must be supplied.

## Record builder

- [`schema`](./p-schema-schemace-schema.md): Record-schema computation expression.
- [`field`](./m-schema-schemace-field.md): Declares a field with an explicit wire name.
- [`construct`](./m-schema-schemace-construct.md): Closes a record schema with a total constructor.
- [`constructResult`](./m-schema-schemace-constructresult.md): Closes a record schema with a checked constructor.

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

- [`Schema.JsonSchema.generate`](./m-schema-jsonschema-generate.md): Generates a compact JSON Schema document from any completed schema declaration.
- [`Schema.JsonSchema.generateValue`](./m-schema-jsonschema-generatevalue.md): Generates a compact JSON Schema document for a standalone value schema.

## Schema derivation attributes (read by schemagen at generation time)

- [`Schema.Derive.DeriveSchemaAttribute`](./t-schema-derive-deriveschemaattribute.md): Marks a plain record for schema derivation: <code>schemagen</code> generates its permissive schema.
 The advice is to put this on wire DTOs — records that carry no invariants of their own. The attributes
 in this namespace are inert metadata: they are read from source text at generation time, never by
 runtime reflection.
- [`Schema.Derive.SchemaNameAttribute`](./t-schema-derive-schemanameattribute.md): <pre>Overrides the external name of one record field or one nullary union case. Without it, field
 names follow the generation run's naming policy (camelCase by default) and case tags are the camelCased
 case name.</pre>
- [`Schema.Derive.DeriveUnionAttribute`](./t-schema-derive-deriveunionattribute.md): Marks a discriminated union as an internally tagged union in the derived schema. Every case
 must carry exactly one <code>[&lt;DeriveSchema&gt;]</code> record payload; the discriminator is the given
 external field name.
- [`Schema.Derive.SchemaConstructorAttribute`](./t-schema-derive-schemaconstructorattribute.md): Marks the static member the derived schema calls to assemble the record, instead of a
 record literal. Put it on one static member of a <code>[&lt;DeriveSchema&gt;]</code> record that takes the
 fields in declaration order and returns the record type; use it to normalise values on the way
 in.
- [`Schema.Derive.PatternAttribute`](./t-schema-derive-patternattribute.md): Constrains a text field to the given regular expression.
- [`Schema.Derive.MinAttribute`](./t-schema-derive-minattribute.md): Bounds the natural length of a text, list, or map field from below.
- [`Schema.Derive.MaxAttribute`](./t-schema-derive-maxattribute.md): Bounds the natural length of a text, list, or map field from above.
- [`Schema.Derive.LengthAttribute`](./t-schema-derive-lengthattribute.md): Requires the natural length of a text, list, or map field to equal the supplied value.
- [`Schema.Derive.LengthBetweenAttribute`](./t-schema-derive-lengthbetweenattribute.md): Bounds the natural length of a text, list, or map field inclusively.
- [`Schema.Derive.PresentAttribute`](./t-schema-derive-presentattribute.md): Requires a string, collection, or optional field value to be present.
- [`Schema.Derive.SuppliedAttribute`](./t-schema-derive-suppliedattribute.md): Requires the input payload to supply the field key.
- [`Schema.Derive.FormatAttribute`](./t-schema-derive-formatattribute.md): Adds open format metadata to the field schema.
- [`Schema.Derive.AtLeastAttribute`](./t-schema-derive-atleastattribute.md): Bounds a numeric field&#39;s value inclusively from below (<code>&gt;=</code> in the contract grammar).
 The literal is read from source text, so decimal precision is preserved exactly.
- [`Schema.Derive.GreaterThanAttribute`](./t-schema-derive-greaterthanattribute.md): Bounds a numeric field&#39;s value exclusively from below (<code>&gt;</code> in the contract grammar).
- [`Schema.Derive.AtMostAttribute`](./t-schema-derive-atmostattribute.md): Bounds a numeric field&#39;s value inclusively from above (<code>&lt;=</code> in the contract grammar).
- [`Schema.Derive.LessThanAttribute`](./t-schema-derive-lessthanattribute.md): Bounds a numeric field&#39;s value exclusively from above (<code>&lt;</code> in the contract grammar).
- [`Schema.Derive.MultipleOfAttribute`](./t-schema-derive-multipleofattribute.md): Constrains a numeric field's value to whole multiples of the given step.
- [`Schema.Derive.DistinctAttribute`](./t-schema-derive-distinctattribute.md): Requires the elements of a list field to be distinct.
- [`Schema.Derive.EmailAttribute`](./t-schema-derive-emailattribute.md): Constrains a text field to the email format.
- [`Schema.Derive.DefaultAttribute`](./t-schema-derive-defaultattribute.md): Supplies the field&#39;s default when the payload omits it. Not valid on optional fields —
 absence already parses to <code>None</code>.
