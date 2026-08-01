---
title: "Schema Interpreters"
weight: 500
---

This page shows structured boundary data, universal schema parsing into `Result`, opt-in input retention with `RetainedParseResult`, checking of existing values, and refined schemas. Core schema metadata stays in [Schema](../); interpreters attach path-aware `SchemaErrors` and optional redisplay behavior to it.

## Structured data

- [`Data`](./t-data.md): A portable tree representing the meaning and shape of unowned structured data.
- [`DataPathSegment`](./t-datapathsegment.md): A segment in a structured data path.
- [`DataPath`](./t-datapath.md): Helpers for constructing, parsing, and rendering structured data paths.
- [`Data.ofMap`](./m-data-ofmap.md): Builds object-shaped data from a map of scalar values.
- [`Data.ofNameValues`](./m-data-ofnamevalues.md): Builds object-shaped data from name and value pairs.
- [`Data.ofCliArgs`](./m-data-ofcliargs.md): Builds structured data from command-line arguments.
- [`Data.ofJsonElement`](./m-data-ofjsonelement.md): Builds owned structured data from a JSON element.
- [`Data.ofJsonDocument`](./m-data-ofjsondocument.md): Builds owned structured data from a JSON document.
- [`Data.ofConfiguration`](./m-data-ofconfiguration.md): Builds structured data from flattened configuration keys.
- [`Data.redisplay`](./m-data-redisplay.md): Renders one scalar value for redisplay.
- [`Data.redisplayPath`](./m-data-redisplaypath.md): Parses a path and redisplays its scalar.

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

- [`Schema.RefinedSchemas.nonBlankString`](./p-schema-refinedschemas-nonblankstring.md):
- [`Schema.RefinedSchemas.finiteFloat`](./p-schema-refinedschemas-finitefloat.md):
- [`Schema.RefinedSchemas.unitInterval`](./p-schema-refinedschemas-unitinterval.md):
- [`Schema.RefinedSchemas.nonEmptyList`](./m-schema-refinedschemas-nonemptylist.md):
- [`Schema.RefinedSchemas.nonEmptyArray`](./m-schema-refinedschemas-nonemptyarray.md):
- [`Schema.RefinedSchemas.distinctList`](./m-schema-refinedschemas-distinctlist.md):
- [`Schema.RefinedSchemas.interval`](./m-schema-refinedschemas-interval.md):
 Builds a schema for an inclusive range, replacing the former per-type range
 schemas. Generic over any ordered value, so one definition covers what
 <code>dateTimeOffsetRange</code> and <code>dateOnlyRange</code> each needed separately.

- [`Schema.RefinedSchemas.dateRange`](./p-schema-refinedschemas-daterange.md):
 Builds a schema for a range of instants using <code>start</code> and <code>end</code> field
 names. The same <code>Interval</code> type as <code>interval</code> above — only the wire
 vocabulary differs, which is why no second type is needed. An inverted pair is
 reported rather than silently reordered, since at a boundary that is a caller error.

- [`Schema.RefinedSchemas.bounded`](./m-schema-refinedschemas-bounded.md):  Builds a schema for a value confined to the supplied bounds. The bounds belong to
 the schema rather than to each value, so they are supplied once here.

## Existing values

- [`Schema.check`](./m-schema-schema-check.md): Checks an existing typed value, such as a freely constructed draft, through the schema's constraints, refinements, and record constructor.
