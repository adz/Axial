---
title: "Schema Interpreters"
weight: 500
type: docs
---

This page shows structured boundary data, universal schema parsing into `Result`, opt-in input retention with `RetainedParseResult`, checking of existing values, and refined schemas. Core schema metadata stays in [Schema](../); interpreters attach path-aware `SchemaErrors` and optional redisplay behavior to it.

## Structured data

- [`Data`](./t-data.md): A portable tree representing the meaning and shape of unowned structured data.
- [`DataPathSegment`](./t-datapathsegment.md): A segment in a structured data path.
- [`DataPath`](./t-datapath.md): Helpers for constructing, parsing, and rendering structured data paths.
- [`Data.ofMap`](./m-data-ofmap.md): Builds object-shaped structured data from a map of scalar field values.
- [`Data.ofNameValues`](./m-data-ofnamevalues.md): Builds object-shaped structured data from name/value pairs, grouping repeated names into <code>Many</code>.
- [`Data.ofCliArgs`](./m-data-ofcliargs.md):
 Builds structured data from command-line arguments.

- [`Data.ofJsonElement`](./m-data-ofjsonelement.md): Builds structured data from a <a href="https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement">JsonElement</a>.
- [`Data.ofJsonDocument`](./m-data-ofjsondocument.md): Builds structured data from the root element of a <a href="https://learn.microsoft.com/dotnet/api/system.text.json.jsondocument">JsonDocument</a>.
- [`Data.ofConfiguration`](./m-data-ofconfiguration.md):
 Builds structured data from flattened configuration keys using <code>:</code> as the path separator.

- [`Data.redisplay`](./m-data-redisplay.md):  Redisplays a scalar structured data value, returning blank text for missing, object-shaped, or collection-shaped input.
- [`Data.redisplayPath`](./m-data-redisplaypath.md): Parses an input path and redisplays the addressed scalar structured data value.

## Input parsing

- [`Schema.parse`](./m-schema-schema-parse.md): Parses source-neutral structured data, runs constraints and refinements, and invokes record constructors.
- [`Schema.parseRetainingInput`](./m-schema-schema-parseretaininginput.md): Parses source-neutral structured data while retaining it for redisplay and error lookup.
- [`Schema.parseWith`](./m-schema-schema-parsewith.md): Parses structured data after configuring parser options.
- [`Schema.SchemaParseOptions`](./t-schema-schemaparseoptions.md): Options that customize how structured data is parsed through a schema.
- [`Schema.RetainedParseResult`](./t-schema-retainedparseresult.md): A schema parse result that retains its original structured input.
- [`Schema.RetainedParseResult.create`](./m-schema-retainedparseresult-create.md): Retains structured data alongside an existing schema parse result.
- [`Schema.RetainedParseResult.renderErrors`](./m-schema-retainedparseresult-rendererrors.md): Renders one line for every failed schema issue.

## Errors

- [`Schema.SchemaError`](./t-schema-schemaerror.md): Schema input, checking, and contextual rule failures attached to diagnostics paths.
- [`Schema.Path`](./t-schema-path.md): An immutable location within structured schema input.
- [`Schema.Path.root`](./m-schema-path-root.md): The root of a schema value.
- [`Schema.Path.key`](./m-schema-path-key.md): A string field or map-key location.
- [`Schema.Path.index`](./m-schema-path-index.md): A zero-based collection-item location.
- [`Schema.Path.append`](./m-schema-path-append.md): Appends a relative path to a parent path.
- [`Schema.Path.format`](./m-schema-path-format.md): Formats a path with dot-separated keys and bracketed indexes.
- [`Schema.Path.fold`](./m-schema-path-fold.md): Folds over string keys and integer indexes without exposing a path-segment type.
- [`Schema.SchemaIssue`](./t-schema-schemaissue.md): One schema failure and its complete structural location.
- [`Schema.SchemaErrors`](./t-schema-schemaerrors.md): One or more accumulated schema failures.
- [`Schema.SchemaErrors.toList`](./m-schema-schemaerrors-tolist.md): Returns failures in deterministic path order.
- [`Schema.SchemaErrors.count`](./m-schema-schemaerrors-count.md): Returns the number of accumulated failures.
- [`Schema.SchemaErrors.isEmpty`](./m-schema-schemaerrors-isempty.md): Reports whether the collection contains no failures.
- [`Schema.SchemaErrors.toString`](./m-schema-schemaerrors-tostring.md): Renders one line per failure.

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
