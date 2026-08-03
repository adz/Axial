---
title: "Result"
weight: 10
type: docs
---

This page shows `Axial.Result`: helpers over the standard F# `Result<'value, 'error>` type. Use `Result.requireTrue` when a bare `bool` condition should become a `Result` (nothing to preserve). Use `Result.okIf`/`Result.failIf` (mirroring `Option.filter`) when a predicate over the value itself should keep that value on success, then attach the real error afterward with `Result.orError`. Extraction helpers such as `Result.someOr` change the success shape. The `result { }` builder sequences fail-fast steps; `result.list { }` and `result.array { }` accumulate independent failures through `and!`. The package is a standalone leaf: for reusable value rules and the structured `Violation` they produce, see the Values reference.

## Core helpers

- [`Result.ok`](./result/m-result-result-ok.md): Creates an <code>Ok</code> result.
- [`Result.error`](./result/m-result-result-error.md): Creates an <code>Error</code> result.
- [`Result.map`](./result/m-result-result-map.md): Maps the success value of a result.
- [`Result.mapError`](./result/m-result-result-maperror.md): Maps the error value of a result.
- [`Result.bind`](./result/m-result-result-bind.md): Binds a result to the next fail-fast operation.
- [`Result.orElse`](./result/m-result-result-orelse.md): Falls back to another result when the source result fails.
- [`Result.orElseWith`](./result/m-result-result-orelsewith.md): Computes a fallback result from the source error when the result fails.

## Lifts and conversions

- [`Result.requireTrue`](./result/m-result-result-requiretrue.md): Returns <code>Ok ()</code> when the condition is true, or the supplied error when it is false.
- [`Result.okIf`](./result/m-result-result-okif.md): Keeps the input value when the predicate holds, or returns the supplied error.
- [`Result.failIf`](./result/m-result-result-failif.md): Keeps the input value when the predicate does not hold, or returns the supplied error.
- [`Result.orError`](./result/m-result-result-orerror.md): Replaces whatever error a result carries with the supplied typed error. <code>Ok</code> passes through unchanged.
- [`Result.fromTry`](./result/m-result-result-fromtry.md): Converts a .NET <code>Try*</code> tuple into a unit-error result.
- [`Result.fromChoice`](./result/m-result-result-fromchoice.md): Converts an F# <code>Choice</code> into a result.
- [`Result.toOption`](./result/m-result-result-tooption.md): Drops the error channel and returns <code>Some</code> for success.
- [`Result.toValueOption`](./result/m-result-result-tovalueoption.md): Drops the error channel and returns <code>ValueSome</code> for success.
- [`Result.defaultValue`](./result/m-result-result-defaultvalue.md): Returns the success value or the supplied fallback value.

## Extraction helpers

- [`Result.someOr`](./result/m-result-result-someor.md): Takes the value from an option when it is <code>Some</code>, or returns the supplied error.
- [`Result.noneOr`](./result/m-result-result-noneor.md): Returns success when the option is <code>None</code>, or returns the supplied error.
- [`Result.valueSomeOr`](./result/m-result-result-valuesomeor.md): Takes the value from a value option when it is <code>ValueSome</code>, or returns the supplied error.
- [`Result.valueNoneOr`](./result/m-result-result-valuenoneor.md): Returns success when the value option is <code>ValueNone</code>, or returns the supplied error.
- [`Result.nullableOr`](./result/m-result-result-nullableor.md): Takes the value from a nullable when it has a value, or returns the supplied error.
- [`Result.notNullOr`](./result/m-result-result-notnullor.md): Keeps a non-null reference, or returns the supplied error.
- [`Result.okOr`](./result/m-result-result-okor.md): Takes the successful value from a result, or returns the supplied error.
- [`Result.errorOr`](./result/m-result-result-erroror.md): Takes the error value from a result, or returns the supplied error when the result is successful.
- [`Result.headOr`](./result/m-result-result-heador.md): Takes the first item from a sequence, or returns the supplied error.

## Traversal

- [`Result.traverse`](./result/m-result-result-traverse.md): Maps each value with a result-returning function, stopping at the first error.
- [`Result.sequence`](./result/m-result-result-sequence.md): Turns a sequence of results into one fail-fast result containing all successes.

## Side effects

- [`Result.tap`](./result/m-result-result-tap.md): Runs a side effect on the successful value and returns the result unchanged.
- [`Result.tapError`](./result/m-result-result-taperror.md): Runs a side effect on the error value and returns the result unchanged.

## Builder

- [`result`](./result-ce/p-result--result.md): The fail-fast <code>result { }</code> computation expression.
