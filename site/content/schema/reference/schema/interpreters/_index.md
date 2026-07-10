---
title: "Schema Interpreters"
weight: 500
type: docs
---

This page shows the `Axial.Schema` interpreter surface: raw boundary input, schema input parsing into `ParsedInput`, intrinsic validation of existing models, and contextual rule sets over already-trusted models. Core schema metadata stays in [Schema](../); these interpreters attach diagnostics, raw input, and redisplay behavior to it.

## Raw input

- [`Schema.RawInput`](./t-schema-rawinput.md):
 Source-agnostic raw input captured at a data boundary before schema parsing and diagnostics interpretation.

- [`Schema.JsonLikeValue`](./t-schema-jsonlikevalue.md): A small dependency-free value model for adapting JSON-shaped data into <a href="t-schema-rawinput.md">RawInput</a>.
- [`Schema.RawInput.ofMap`](./m-schema-rawinput-ofmap.md): Builds object-shaped raw input from a map of scalar field values.
- [`Schema.RawInput.ofNameValues`](./m-schema-rawinput-ofnamevalues.md): Builds object-shaped raw input from name/value pairs, grouping repeated names into <code>Many</code>.
- [`Schema.RawInput.ofCliArgs`](./m-schema-rawinput-ofcliargs.md):
 Builds raw input from command-line arguments.

- [`Schema.RawInput.ofJsonLikeValue`](./m-schema-rawinput-ofjsonlikevalue.md): Builds raw input from dependency-free JSON-shaped values.
- [`Schema.RawInput.ofJsonElement`](./m-schema-rawinput-ofjsonelement.md): Builds raw input from a <a href="https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement">JsonElement</a>.
- [`Schema.RawInput.ofJsonDocument`](./m-schema-rawinput-ofjsondocument.md): Builds raw input from the root element of a <a href="https://learn.microsoft.com/dotnet/api/system.text.json.jsondocument">JsonDocument</a>.
- [`Schema.RawInput.ofConfiguration`](./m-schema-rawinput-ofconfiguration.md):
 Builds raw input from flattened configuration keys using <code>:</code> as the path separator.

- [`Schema.RawInput.redisplay`](./m-schema-rawinput-redisplay.md):  Redisplays a scalar raw input value, returning blank text for missing, object-shaped, or collection-shaped input.
- [`Schema.RawInput.redisplayPath`](./m-schema-rawinput-redisplaypath.md): Parses an input path and redisplays the addressed scalar raw input value.

## Input parsing

- [`Schema.ParsedInput`](./t-schema-parsedinput.md):
 The result of parsing boundary input through a schema while retaining the original raw input.

- [`Schema.Input.parse`](./m-schema-input-parse.md): Parses raw boundary input through a trusted model schema.
- [`Schema.Input.parseWith`](./m-schema-input-parsewith.md): Parses raw boundary input through a trusted model schema using custom input parser options.
- [`Schema.Input.Options`](./t-schema-input-options.md): Options that customize how raw input is parsed through a schema.
- [`Schema.ParsedInput.mapErrors`](./m-schema-parsedinput-maperrors.md): Maps a failed parse&#39;s errors to a domain or application error type, preserving the raw input and paths.
- [`Schema.ParsedInput.renderErrors`](./m-schema-parsedinput-rendererrors.md): Renders a failed schema parse as default English display strings, preserving diagnostics paths.

## Errors

- [`Schema.SchemaError`](./t-schema-schemaerror.md): Schema input, model validation, and contextual rule failures attached to diagnostics paths.

## Refined catalog schemas

- [`Schema.RefinedSchema.nonBlankString`](./m-schema-refinedschema-nonblankstring.md): Describes a non-blank string as a schema refined value over required text.
- [`Schema.RefinedSchema.trimmedString`](./m-schema-refinedschema-trimmedstring.md): Describes a trimmed string as a schema refined value over text with no leading or trailing whitespace.
- [`Schema.RefinedSchema.boundedString`](./m-schema-refinedschema-boundedstring.md): Describes a bounded string as a schema refined value over required text with inclusive length bounds.
- [`Schema.RefinedSchema.slug`](./m-schema-refinedschema-slug.md): Describes an ASCII slug as a schema refined value over required text with the built-in slug pattern.
- [`Schema.RefinedSchema.positiveInt`](./m-schema-refinedschema-positiveint.md): Describes a positive integer as a schema refined value over an integer greater than zero.
- [`Schema.RefinedSchema.nonNegativeInt`](./m-schema-refinedschema-nonnegativeint.md): Describes a non-negative integer as a schema refined value over an integer greater than or equal to zero.
- [`Schema.RefinedSchema.nonZeroInt`](./m-schema-refinedschema-nonzeroint.md): Describes a non-zero integer as a schema refined value over an integer not equal to zero.
- [`Schema.RefinedSchema.negativeInt`](./m-schema-refinedschema-negativeint.md): Describes a negative integer as a schema refined value over an integer less than zero.
- [`Schema.RefinedSchema.nonPositiveInt`](./m-schema-refinedschema-nonpositiveint.md): Describes a non-positive integer as a schema refined value over an integer less than or equal to zero.
- [`Schema.RefinedSchema.nonEmptyList`](./m-schema-refinedschema-nonemptylist.md): Describes a non-empty list as a schema refined value over a collection of item schemas.
- [`Schema.RefinedSchema.nonEmptyArray`](./m-schema-refinedschema-nonemptyarray.md): Describes a non-empty array as a schema refined value over a collection of item schemas.
- [`Schema.RefinedSchema.distinctList`](./m-schema-refinedschema-distinctlist.md): Describes a distinct list as a schema refined value over a distinct collection of item schemas.
- [`Schema.RefinedSchema.boundedList`](./m-schema-refinedschema-boundedlist.md): Describes a bounded list as a schema refined value over a collection with inclusive count bounds.
- [`Schema.RefinedSchema.boundedArray`](./m-schema-refinedschema-boundedarray.md): Describes a bounded array as a schema refined value over a collection with inclusive count bounds.
- [`Schema.RefinedSchema.dateTimeOffsetRange`](./m-schema-refinedschema-datetimeoffsetrange.md): Describes a date-time range as a record schema with <code>start</code> and <code>end</code> fields.

## Model validation

- [`Schema.Validation.validate`](./m-schema-validation-validate.md): Validates an existing trusted model value through a built model schema.

## Rules

- [`Schema.RuleSet`](./t-schema-ruleset.md):
 A collection of contextual rules evaluated over an already-trusted model.

- [`Schema.Rules.create`](./m-schema-rules-create.md): Creates a contextual rule set from one executable model rule.
- [`Schema.Rules.concat`](./m-schema-rules-concat.md): Combines contextual rule sets in sequence, preserving rule order.
- [`Schema.Rules.at`](./m-schema-rules-at.md): Scopes a rule&#39;s diagnostics under the supplied path when the rule fails.
- [`Schema.Rules.failAt`](./m-schema-rules-failat.md): Creates a rule failure attached to the supplied diagnostics path.
- [`Schema.Rules.custom`](./m-schema-rules-custom.md): Creates a custom schema rule error with a stable code and display message.
- [`Schema.Rules.validate`](./m-schema-rules-validate.md): Evaluates contextual rules over an already-trusted model.
- [`Schema.Rules.apply`](./m-schema-rules-apply.md): Applies contextual rules to an already-trusted model, returning a plain result.
