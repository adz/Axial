---
title: "Validation"
weight: 40
type: docs
---

This page shows the `Validation<'value, 'error>` surface for accumulating several failures into one diagnostics graph. Unlike `Result`, validation does not stop at the first independent error; functions such as `map2`, `map3`, `apply`, `collect`, and `traverseIndexed` combine sibling checks and preserve all reported problems. Use `Validation.fromResult` as the canonical bridge from fail-fast `Result` values into validation, and use `Validation.toResult` when a boundary expects ordinary `Result`. Use path helpers such as `name`, `key`, `index`, and `at` to attach errors to fields, map entries, list positions, or nested structures. Use `Validation` for input decoding, command validation, configuration checks, and any boundary where users need a complete error report.

## Core type

- [`Validation.Validation`](./t-validation-validation.md):
 An accumulating validation result that keeps the structured diagnostics graph visible.


## Module functions

- [`Validation.Validation.toResult`](./m-validation-validation-toresult.md): Converts a <a href="https://learn.microsoft.com/dotnet/api/axial.validation-2">Validation</a> into a standard <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a>.
- [`Validation.Validation.ok`](./m-validation-validation-ok.md): Creates a successful validation result.
- [`Validation.Validation.error`](./m-validation-validation-error.md): Creates a failing validation result with the provided diagnostics.
- [`Validation.Validation.succeed`](./m-validation-validation-succeed.md): Alias for <code>ok</code>.
- [`Validation.Validation.fail`](./m-validation-validation-fail.md): Alias for <code>error</code>.
- [`Validation.Validation.fromResult`](./m-validation-validation-fromresult.md): Lifts a standard <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a> into the <a href="https://learn.microsoft.com/dotnet/api/axial.validation-2">Validation</a> context.
- [`Validation.Validation.map`](./m-validation-validation-map.md): Maps the successful value of a validation.
- [`Validation.Validation.bind`](./m-validation-validation-bind.md): Sequences a validation-producing continuation.
- [`Validation.Validation.mapError`](./m-validation-validation-maperror.md): Maps the error type of a validation graph.
- [`Validation.Validation.map2`](./m-validation-validation-map2.md): Combines two validations, accumulating errors if both fail.
- [`Validation.Validation.map3`](./m-validation-validation-map3.md): Combines three validations, accumulating errors when any input fails.
- [`Validation.Validation.apply`](./m-validation-validation-apply.md): Applies a validation-wrapped function to a validation-wrapped value.
- [`Validation.Validation.ignore`](./m-validation-validation-ignore.md): Maps a successful validation value to <code>unit</code> while preserving the diagnostics.
- [`Validation.Validation.orElse`](./m-validation-validation-orelse.md): Falls back to another validation when the source validation fails.
- [`Validation.Validation.orElseWith`](./m-validation-validation-orelsewith.md): Computes a fallback validation from the source diagnostics when validation fails.
- [`Validation.Validation.collect`](./m-validation-validation-collect.md): Collects a sequence of validations into a single validation of a list.
- [`Validation.Validation.sequence`](./m-validation-validation-sequence.md): Transforms a sequence of validations into a validation of a list.
- [`Validation.Validation.traverseIndexed`](./m-validation-validation-traverseindexed.md): Maps a sequence into validations while prefixing each item with its index.
- [`Validation.Validation.merge`](./m-validation-validation-merge.md): Merges two validations into a validation of a tuple.

## Path scoping

- [`Validation.Validation.at`](./m-validation-validation-at.md): Scopes a validation under the supplied path segments.
- [`Validation.Validation.key`](./m-validation-validation-key.md): Prefixes a validation with a keyed branch.
- [`Validation.Validation.index`](./m-validation-validation-index.md): Prefixes a validation with an indexed branch.
- [`Validation.Validation.name`](./m-validation-validation-name.md): Prefixes a validation with a named branch.
