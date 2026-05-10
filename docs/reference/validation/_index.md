---
title: "Validation"
---

This page shows the source-documented `Validation` surface: the accumulating result type, module functions, and path-scoping helpers.

## Core type

- [`FsFlow.Validation`](./t-validation.md): An accumulating validation result that keeps the structured diagnostics graph visible.

## Module functions

- [`FsFlow.ValidationModule.toResult`](./m-validationmodule-toresult.md): Converts a `Validation` into a standard `Result`.
- [`FsFlow.ValidationModule.ok`](./m-validationmodule-ok.md): Creates a successful validation result.
- [`FsFlow.ValidationModule.error`](./m-validationmodule-error.md): Creates a failing validation result with the provided diagnostics.
- [`FsFlow.ValidationModule.succeed`](./m-validationmodule-succeed.md): Alias for `ok`.
- [`FsFlow.ValidationModule.fail`](./m-validationmodule-fail.md): Alias for `error`.
- [`FsFlow.ValidationModule.fromResult`](./m-validationmodule-fromresult.md): Lifts a standard `Result` into the `Validation` context.
- [`FsFlow.ValidationModule.map`](./m-validationmodule-map.md): Maps the successful value of a validation.
- [`FsFlow.ValidationModule.bind`](./m-validationmodule-bind.md): Sequences a validation-producing continuation.
- [`FsFlow.ValidationModule.mapError`](./m-validationmodule-maperror.md): Maps the error type of a validation graph.
- [`FsFlow.ValidationModule.map2`](./m-validationmodule-map2.md): Combines two validations, accumulating errors if both fail.
- [`FsFlow.ValidationModule.map3`](./m-validationmodule-map3.md): Combines three validations, accumulating errors when any input fails.
- [`FsFlow.ValidationModule.apply`](./m-validationmodule-apply.md): Applies a validation-wrapped function to a validation-wrapped value.
- [`FsFlow.ValidationModule.ignore`](./m-validationmodule-ignore.md): Maps a successful validation value to `unit` while preserving the diagnostics.
- [`FsFlow.ValidationModule.orElse`](./m-validationmodule-orelse.md): Falls back to another validation when the source validation fails.
- [`FsFlow.ValidationModule.orElseWith`](./m-validationmodule-orelsewith.md): Computes a fallback validation from the source diagnostics when validation fails.
- [`FsFlow.ValidationModule.collect`](./m-validationmodule-collect.md): Collects a sequence of validations into a single validation of a list.
- [`FsFlow.ValidationModule.sequence`](./m-validationmodule-sequence.md): Transforms a sequence of validations into a validation of a list.
- [`FsFlow.ValidationModule.traverseIndexed`](./m-validationmodule-traverseindexed.md): Maps a sequence into validations while prefixing each item with its index.
- [`FsFlow.ValidationModule.merge`](./m-validationmodule-merge.md): Merges two validations into a validation of a tuple.

## Path scoping

- [`FsFlow.ValidationModule.at`](./m-validationmodule-at.md): Scopes a validation under the supplied path segments.
- [`FsFlow.ValidationModule.key`](./m-validationmodule-key.md): Prefixes a validation with a keyed branch.
- [`FsFlow.ValidationModule.index`](./m-validationmodule-index.md): Prefixes a validation with an indexed branch.
- [`FsFlow.ValidationModule.name`](./m-validationmodule-name.md): Prefixes a validation with a named branch.

