---
title: "Data"
weight: 30
type: docs
---

This page shows `Axial.Data`: one owned tree for literals, source adapters, immutable edits, named cases, exact differences, and produced-data proofs. It has no dependencies on other Axial packages.

## The tree

- [`Data`](./t-data.md): A portable tree representing the meaning and shape of unowned structured data.
- [`DataPathSegment`](./t-datapathsegment.md): A segment in a structured data path.
- [`DataPath`](./t-datapath.md): Helpers for constructing, parsing, and rendering structured data paths.

## Constructors

- [`Data.ofMap`](./m-data-ofmap.md): Builds object-shaped data from a map of scalar values.
- [`Data.ofNameValues`](./m-data-ofnamevalues.md): Builds object-shaped data from name and value pairs.
- [`Data.ofCliArgs`](./m-data-ofcliargs.md): Builds structured data from command-line arguments.
- [`Data.ofJsonElement`](./m-data-ofjsonelement.md): Builds owned structured data from a JSON element.
- [`Data.ofJsonDocument`](./m-data-ofjsondocument.md): Builds owned structured data from a JSON document.
- [`Data.ofConfiguration`](./m-data-ofconfiguration.md): Builds structured data from flattened configuration keys.

## Literal syntax

- [`DataField`](./t-datafield.md): An opaque object-field instruction shared by data literals and object patterns.
- [`Data.Syntax.data`](./m-data-syntax-data.md): Builds an object from ordered field instructions.
- [`Data.Syntax.(=>)`](./m-data-syntax-op_equalsgreater.md): Associates a field name with an exact value or recursive data pattern.
- [`Data.Syntax.(?=>)`](./m-data-syntax-op_qmarkequalsgreater.md): Associates a field name with an optional exact value, omitting <code>None</code>.
- [`Data.Syntax.nil`](./p-data-syntax-nil.md): An explicit structured null used by literals and edits.
- [`Data.Syntax.num`](./m-data-syntax-num.md): Constructs an exact number from a validated portable JSON number token.
- [`Data.Syntax.fields`](./m-data-syntax-fields.md): Returns exact field instructions for spreading an existing object literal.

## Edits

- [`DataEdit`](./t-dataedit.md): An opaque immutable edit applied by <code>Data.tryPatch</code> or <code>patch</code>.
- [`DataPatchFailure`](./t-datapatchfailure.md): Describes why one immutable data edit could not be applied.
- [`DataPatchException`](./t-datapatchexception.md): Raised by authored patch syntax when an edit cannot be applied.
- [`Data.Syntax.set`](./m-data-syntax-set.md): Replaces an existing value.
- [`Data.Syntax.put`](./m-data-syntax-put.md): Replaces a final value or appends a missing final object field.
- [`Data.Syntax.remove`](./m-data-syntax-remove.md): Removes an existing field or list item.
- [`Data.Syntax.append`](./m-data-syntax-append.md): Appends an item to an existing list.
- [`Data.Syntax.prepend`](./m-data-syntax-prepend.md): Prepends an item to an existing list.
- [`Data.Syntax.insert`](./m-data-syntax-insert.md): Inserts an item at a valid list insertion index.
- [`Data.Syntax.rename`](./m-data-syntax-rename.md): Renames an existing object field without moving it.
- [`Data.Syntax.update`](./m-data-syntax-update.md): Applies an ordinary function to an existing value.
- [`Data.Syntax.patch`](./m-data-syntax-patch.md): Applies authored edits or raises <code>DataPatchException</code>.
- [`Data.tryPatch`](./m-data-trypatch.md): Applies immutable edits atomically in declaration order.

## Cases

- [`DataVariation`](./t-datavariation.md): A named immutable variation from one baseline value.
- [`DataCase`](./t-datacase.md): A named materialized structured-data case.
- [`DataDimension`](./t-datadimension.md): One independent axis in a bounded Cartesian data matrix.
- [`Data.Syntax.variant`](./m-data-syntax-variant.md): Declares one named variation from a baseline.
- [`Data.Syntax.variants`](./m-data-syntax-variants.md): Materializes named variations from one baseline.
- [`Data.Syntax.dimension`](./m-data-syntax-dimension.md): Declares one named dimension in a Cartesian matrix.
- [`Data.Syntax.matrix`](./m-data-syntax-matrix.md): Materializes a deterministic Cartesian matrix, limited to 256 cases.

## Comparison and matching

- [`DataDifference`](./t-datadifference.md): One focused difference between expected and actual structured data.
- [`DataDifferenceCause`](./t-datadifferencecause.md): The reason an exact structural comparison differed.
- [`DataPattern`](./t-datapattern.md): An opaque recursive expectation used to match structured data.
- [`DataExpectation`](./t-dataexpectation.md): An opaque path-based expectation.
- [`DataMismatch`](./t-datamismatch.md): One failed selective or recursive data expectation.
- [`DataMatchException`](./t-datamatchexception.md): Raised by authored matching syntax when one or more expectations fail.
- [`Data.diff`](./m-data-diff.md): Returns all exact structural differences between two values.
- [`Data.compare`](./m-data-compare.md): Compares complete values and returns every structural difference.
- [`Data.tryMatch`](./m-data-trymatch.md): Checks path-based expectations and accumulates structured mismatches.
- [`Data.Syntax.at`](./m-data-syntax-at.md): Requires a path to contain an exact value or recursive pattern.
- [`Data.Syntax.absent`](./m-data-syntax-absent.md): Requires a path to be absent.
- [`Data.Syntax.matching`](./m-data-syntax-matching.md): Checks authored expectations or raises <code>DataMatchException</code>.
- [`Data.Syntax.exactly`](./m-data-syntax-exactly.md): Creates an exact recursive pattern.
- [`Data.Syntax.containing`](./m-data-syntax-containing.md): Creates a partial object pattern from required fields.
- [`Data.Syntax.containingItems`](./m-data-syntax-containingitems.md): Matches expected items as an unordered consumed subset.
- [`Data.Syntax.inOrder`](./m-data-syntax-inorder.md): Matches expected items as an ordered subsequence.
- [`Data.Syntax.allItems`](./m-data-syntax-allitems.md): Requires every actual list item to satisfy a pattern.
- [`Data.Syntax.someItem`](./m-data-syntax-someitem.md): Requires at least one actual list item to satisfy a pattern.
- [`Data.Syntax.any`](./p-data-syntax-any.md): Matches any present value.
- [`Data.Syntax.anyText`](./p-data-syntax-anytext.md): Matches any text value.
- [`Data.Syntax.anyNumber`](./p-data-syntax-anynumber.md): Matches any number token.
- [`Data.Syntax.oneOf`](./m-data-syntax-oneof.md): Matches when one supplied alternative matches.
- [`Data.Syntax.satisfying`](./m-data-syntax-satisfying.md): Matches an ordinary predicate and uses its description in diagnostics.

## JSON

- [`Data.Json.parse`](./m-data-json-parse.md): Parses one JSON value into owned structured data.
- [`Data.Json.render`](./p-data-json-render.md): Renders compact deterministic JSON.
- [`Data.Json.renderIndented`](./p-data-json-renderindented.md): Renders indented deterministic JSON.

## Rendering and extraction

- [`Data.render`](./m-data-render.md): Renders structured data as deterministic compact JSON.
- [`Data.renderIndented`](./m-data-renderindented.md): Renders structured data as deterministic indented JSON.
- [`Data.tryText`](./m-data-trytext.md): Attempts to extract text from one structured value.
- [`Data.tryBool`](./m-data-trybool.md): Attempts to extract a Boolean from one structured value.
- [`Data.tryNumberToken`](./m-data-trynumbertoken.md): Attempts to extract the preserved token from one number value.
- [`Data.tryList`](./m-data-trylist.md): Attempts to extract items from one list value.
- [`Data.tryObject`](./m-data-tryobject.md): Attempts to extract ordered fields from one object value.

## Redisplay

- [`Data.redisplay`](./m-data-redisplay.md): Renders one scalar value for redisplay.
- [`Data.redisplayPath`](./m-data-redisplaypath.md): Parses a path and redisplays its scalar.
