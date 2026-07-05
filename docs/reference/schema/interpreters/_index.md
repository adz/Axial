---
title: "Schema Interpreters"
weight: 500
---

This page shows the `Axial.Validation.Schema` interpreter surface: raw boundary input, schema input parsing into `ParsedInput`, intrinsic validation of existing models, and contextual rule sets over already-trusted models. Core schema metadata stays in [Schema](../); these interpreters attach diagnostics, raw input, and redisplay behavior to it.

## Raw input

- [`Validation.Schema.RawInput`](./t-validation-schema-rawinput.md):
 Source-agnostic raw input captured at a data boundary before schema parsing and diagnostics interpretation.

- [`Validation.Schema.JsonLikeValue`](./t-validation-schema-jsonlikevalue.md): A small dependency-free value model for adapting JSON-shaped data into <a href="t-validation-schema-rawinput.md">RawInput</a>.
- [`Validation.Schema.RawInput.ofMap`](./m-validation-schema-rawinput-ofmap.md): Builds object-shaped raw input from a map of scalar field values.
- [`Validation.Schema.RawInput.ofNameValues`](./m-validation-schema-rawinput-ofnamevalues.md): Builds object-shaped raw input from name/value pairs, grouping repeated names into <code>Many</code>.
- [`Validation.Schema.RawInput.ofCliArgs`](./m-validation-schema-rawinput-ofcliargs.md):
 Builds raw input from command-line arguments.

- [`Validation.Schema.RawInput.ofJsonLikeValue`](./m-validation-schema-rawinput-ofjsonlikevalue.md): Builds raw input from dependency-free JSON-shaped values.
- [`Validation.Schema.RawInput.ofConfiguration`](./m-validation-schema-rawinput-ofconfiguration.md):
 Builds raw input from flattened configuration keys using <code>:</code> as the path separator.

- [`Validation.Schema.RawInput.redisplay`](./m-validation-schema-rawinput-redisplay.md):  Redisplays a scalar raw input value, returning blank text for missing, object-shaped, or collection-shaped input.
- [`Validation.Schema.RawInput.redisplayPath`](./m-validation-schema-rawinput-redisplaypath.md): Parses an input path and redisplays the addressed scalar raw input value.

## Input parsing

- [`Validation.Schema.ParsedInput`](./t-validation-schema-parsedinput.md):
 The result of parsing boundary input through a schema while retaining the original raw input.

- [`Validation.Schema.Input.parse`](./m-validation-schema-input-parse.md): Parses raw boundary input through a trusted model schema.
- [`Validation.Schema.Input.parseWith`](./m-validation-schema-input-parsewith.md): Parses raw boundary input through a trusted model schema using custom input parser options.
- [`Validation.Schema.Input.Options`](./t-validation-schema-input-options.md): Options that customize how raw input is parsed through a schema.
- [`Validation.Schema.ParsedInput.mapErrors`](./m-validation-schema-parsedinput-maperrors.md): Maps a failed parse&#39;s errors to a domain or application error type, preserving the raw input and paths.
- [`Validation.Schema.ParsedInput.renderErrors`](./m-validation-schema-parsedinput-rendererrors.md): Renders a failed schema parse as default English display strings, preserving diagnostics paths.

## Errors

- [`Validation.Schema.SchemaError`](./t-validation-schema-schemaerror.md): Schema input, model validation, and contextual rule failures attached to diagnostics paths.

## Refined catalog schemas

- [`Validation.Schema.RefinedSchema.nonBlankString`](./m-validation-schema-refinedschema-nonblankstring.md): Describes a non-blank string as a schema refined value over required text.
- [`Validation.Schema.RefinedSchema.trimmedString`](./m-validation-schema-refinedschema-trimmedstring.md): Describes a trimmed string as a schema refined value over text with no leading or trailing whitespace.
- [`Validation.Schema.RefinedSchema.boundedString`](./m-validation-schema-refinedschema-boundedstring.md): Describes a bounded string as a schema refined value over required text with inclusive length bounds.
- [`Validation.Schema.RefinedSchema.slug`](./m-validation-schema-refinedschema-slug.md): Describes an ASCII slug as a schema refined value over required text with the built-in slug pattern.
- [`Validation.Schema.RefinedSchema.positiveInt`](./m-validation-schema-refinedschema-positiveint.md): Describes a positive integer as a schema refined value over an integer greater than zero.
- [`Validation.Schema.RefinedSchema.nonNegativeInt`](./m-validation-schema-refinedschema-nonnegativeint.md): Describes a non-negative integer as a schema refined value over an integer greater than or equal to zero.
- [`Validation.Schema.RefinedSchema.nonZeroInt`](./m-validation-schema-refinedschema-nonzeroint.md): Describes a non-zero integer as a schema refined value over an integer not equal to zero.
- [`Validation.Schema.RefinedSchema.negativeInt`](./m-validation-schema-refinedschema-negativeint.md): Describes a negative integer as a schema refined value over an integer less than zero.
- [`Validation.Schema.RefinedSchema.nonPositiveInt`](./m-validation-schema-refinedschema-nonpositiveint.md): Describes a non-positive integer as a schema refined value over an integer less than or equal to zero.
- [`Validation.Schema.RefinedSchema.nonEmptyList`](./m-validation-schema-refinedschema-nonemptylist.md): Describes a non-empty list as a schema refined value over a collection of item schemas.
- [`Validation.Schema.RefinedSchema.nonEmptyArray`](./m-validation-schema-refinedschema-nonemptyarray.md): Describes a non-empty array as a schema refined value over a collection of item schemas.
- [`Validation.Schema.RefinedSchema.distinctList`](./m-validation-schema-refinedschema-distinctlist.md): Describes a distinct list as a schema refined value over a distinct collection of item schemas.
- [`Validation.Schema.RefinedSchema.boundedList`](./m-validation-schema-refinedschema-boundedlist.md): Describes a bounded list as a schema refined value over a collection with inclusive count bounds.
- [`Validation.Schema.RefinedSchema.boundedArray`](./m-validation-schema-refinedschema-boundedarray.md): Describes a bounded array as a schema refined value over a collection with inclusive count bounds.
- [`Validation.Schema.RefinedSchema.dateTimeOffsetRange`](./m-validation-schema-refinedschema-datetimeoffsetrange.md): Describes a date-time range as a record schema with <code>start</code> and <code>end</code> fields.

## Model validation

- [`Validation.Schema.Validation.validate`](./m-validation-schema-validation-validate.md): Validates an existing trusted model value through a built model schema.

## Rules

- [`Validation.Schema.RuleSet`](./t-validation-schema-ruleset.md):
 A collection of contextual rules evaluated over an already-trusted model.

- [`Validation.Schema.Rules.create`](./m-validation-schema-rules-create.md): Creates a contextual rule set from one executable model rule.
- [`Validation.Schema.Rules.concat`](./m-validation-schema-rules-concat.md): Combines contextual rule sets in sequence, preserving rule order.
- [`Validation.Schema.Rules.at`](./m-validation-schema-rules-at.md): Scopes a rule&#39;s diagnostics under the supplied path when the rule fails.
- [`Validation.Schema.Rules.failAt`](./m-validation-schema-rules-failat.md): Creates a rule failure attached to the supplied diagnostics path.
- [`Validation.Schema.Rules.custom`](./m-validation-schema-rules-custom.md): Creates a custom schema rule error with a stable code and display message.
- [`Validation.Schema.Rules.validate`](./m-validation-schema-rules-validate.md): Evaluates contextual rules over an already-trusted model.
- [`Validation.Schema.Rules.apply`](./m-validation-schema-rules-apply.md): Applies contextual rules to an already-trusted model, returning a plain result.
