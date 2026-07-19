---
title: "Schema Interpreters"
weight: 500
type: docs
---

This page shows raw boundary input, universal schema parsing into `Result`, opt-in input retention with `RetainedParseResult`, checking of existing values, refined schemas, and contextual rules. Core schema metadata stays in [Schema](../); interpreters attach diagnostics and optional redisplay behavior to it.

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

- [`Schema.parse`](./m-schema-schema-parse.md): Parses source-neutral raw input, runs constraints and refinements, and invokes record constructors.
- [`Schema.parseRetainingInput`](./m-schema-schema-parseretaininginput.md): Parses source-neutral raw input while retaining it for redisplay and error lookup.
- [`Schema.parseWith`](./m-schema-schema-parsewith.md): Parses raw input after configuring parser options.
- [`Schema.SchemaParseOptions`](./t-schema-schemaparseoptions.md): Options that customize how raw input is parsed through a schema.
- [`Schema.RetainedParseResult`](./t-schema-retainedparseresult.md):
 A parse result that retains the original raw input for redisplay and error lookup.

- [`Schema.RetainedParseResult.create`](./m-schema-retainedparseresult-create.md): Retains raw input alongside an existing parse result.
- [`Schema.RetainedParseResult.mapErrors`](./m-schema-retainedparseresult-maperrors.md): Maps a failed parse&#39;s errors to a domain or application error type, preserving the raw input and paths.
- [`Schema.RetainedParseResult.renderErrors`](./m-schema-retainedparseresult-rendererrors.md): Renders a failed schema parse as default English display strings, preserving diagnostics paths.

## Errors

- [`Schema.SchemaError`](./t-schema-schemaerror.md): Schema input, checking, and contextual rule failures attached to diagnostics paths.

## Refined catalog schemas

- [`Schema.RefinedSchemas.nonBlankString`](./p-schema-refinedschemas-nonblankstring.md): Describes a non-blank string as a schema refined value over required text.
- [`Schema.RefinedSchemas.trimmedString`](./p-schema-refinedschemas-trimmedstring.md): Describes a trimmed string as a schema refined value over text with no leading or trailing whitespace.
- [`Schema.RefinedSchemas.boundedString`](./m-schema-refinedschemas-boundedstring.md): Describes a bounded string as a schema refined value over required text with inclusive length bounds.
- [`Schema.RefinedSchemas.slug`](./p-schema-refinedschemas-slug.md): Describes an ASCII slug as a schema refined value over required text with the built-in slug pattern.
- [`Schema.RefinedSchemas.positiveInt`](./p-schema-refinedschemas-positiveint.md): Describes a positive integer as a schema refined value over an integer greater than zero.
- [`Schema.RefinedSchemas.nonNegativeInt`](./p-schema-refinedschemas-nonnegativeint.md): Describes a non-negative integer as a schema refined value over an integer greater than or equal to zero.
- [`Schema.RefinedSchemas.nonZeroInt`](./p-schema-refinedschemas-nonzeroint.md): Describes a non-zero integer as a schema refined value over an integer not equal to zero.
- [`Schema.RefinedSchemas.negativeInt`](./p-schema-refinedschemas-negativeint.md): Describes a negative integer as a schema refined value over an integer less than zero.
- [`Schema.RefinedSchemas.nonPositiveInt`](./p-schema-refinedschemas-nonpositiveint.md): Describes a non-positive integer as a schema refined value over an integer less than or equal to zero.
- [`Schema.RefinedSchemas.nonEmptyList`](./m-schema-refinedschemas-nonemptylist.md): Describes a non-empty list as a schema refined value over a collection of item schemas.
- [`Schema.RefinedSchemas.nonEmptyArray`](./m-schema-refinedschemas-nonemptyarray.md): Describes a non-empty array as a schema refined value over a collection of item schemas.
- [`Schema.RefinedSchemas.distinctList`](./m-schema-refinedschemas-distinctlist.md): Describes a distinct list as a schema refined value over a distinct collection of item schemas.
- [`Schema.RefinedSchemas.boundedList`](./m-schema-refinedschemas-boundedlist.md): Describes a bounded list as a schema refined value over a collection with inclusive count bounds.
- [`Schema.RefinedSchemas.boundedArray`](./m-schema-refinedschemas-boundedarray.md): Describes a bounded array as a schema refined value over a collection with inclusive count bounds.
- [`Schema.RefinedSchemas.dateTimeOffsetRange`](./p-schema-refinedschemas-datetimeoffsetrange.md): Describes a date-time range as a record schema with <code>start</code> and <code>end</code> fields.

## Existing values

- [`Schema.check`](./m-schema-schema-check.md): Checks an existing typed value, such as a freely constructed draft, through the schema's constraints, refinements, and record constructor.
- [`Schema.FieldRef`](./t-schema-fieldref.md): A typed, named reference to one field of a schema-described model.

## Context rules

- [`Schema.ContextRules.apply`](./m-schema-contextrules-apply.md): Applies contextual rules to an already-trusted model, accumulating any diagnostics.
- [`Schema.ContextRules.at`](./m-schema-contextrules-at.md): Scopes a rule&#39;s diagnostics under the supplied path when the rule fails.
- [`Schema.ContextRules.atField`](./m-schema-contextrules-atfield.md): Scopes a rule&#39;s diagnostics under a schema field reference when the rule fails.
- [`Schema.ContextRules.failAt`](./m-schema-contextrules-failat.md): Creates a rule failure attached to the supplied diagnostics path.
- [`Schema.ContextRules.failAtField`](./m-schema-contextrules-failatfield.md): Creates a rule failure attached to a schema field reference&#39;s diagnostics path.
- [`Schema.ContextRules.custom`](./m-schema-contextrules-custom.md): Creates a custom schema rule error with a stable code and display message.
