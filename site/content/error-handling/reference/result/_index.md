---
title: "Result"
weight: 20
type: docs
---

This page shows Axial's fail-fast helpers over the standard F# `Result<'value, 'error>` type. Use `Result.requireTrue` when a bare `bool` condition should become a `Result` (nothing to preserve). Use `Result.okIf`/`Result.failIf` (mirroring `Option.filter`) when a predicate over the value itself should keep that value on success, then attach the real error afterward with `Result.orError`. Extraction helpers such as `Result.someOr` change the success shape. For domain checks with a built-in error type, reach for `Check.*` directly — it is already value-preserving, so no separate `Result` wrapper is needed. Sequence cardinality extraction (`exactlyOne`, `atMostOne`) lives on [Refine]({{< relref "/error-handling/reference/refined/" >}}) instead, since it is a structural refinement, not a generic Result concern. The `result { }` builder sequences ordinary fail-fast `Result` workflows.

## Structured errors

- [`ErrorHandling.CheckFailure`](./t-errorhandling-checkfailure.md): Describes why an executable value check failed, without attaching source paths or structured data.

## Core helpers

- [`ErrorHandling.Result.ok`](./m-errorhandling-result-ok.md): Creates an <code>Ok</code> result.
- [`ErrorHandling.Result.error`](./m-errorhandling-result-error.md): Creates an <code>Error</code> result.
- [`ErrorHandling.Result.map`](./m-errorhandling-result-map.md): Maps the success value of a result.
- [`ErrorHandling.Result.mapError`](./m-errorhandling-result-maperror.md): Maps the error value of a result.
- [`ErrorHandling.Result.bind`](./m-errorhandling-result-bind.md): Binds a result to the next fail-fast operation.
- [`ErrorHandling.Result.orElse`](./m-errorhandling-result-orelse.md): Falls back to another result when the source result fails.
- [`ErrorHandling.Result.orElseWith`](./m-errorhandling-result-orelsewith.md): Computes a fallback result from the source error when the result fails.

## Lifts and conversions

- [`ErrorHandling.Result.requireTrue`](./m-errorhandling-result-requiretrue.md): Returns <code>Ok ()</code> when the condition is true, or the supplied error when it is false.
- [`ErrorHandling.Result.okIf`](./m-errorhandling-result-okif.md): Keeps the input value when the predicate holds, or returns the supplied error.
- [`ErrorHandling.Result.failIf`](./m-errorhandling-result-failif.md): Keeps the input value when the predicate does not hold, or returns the supplied error.
- [`ErrorHandling.Result.orError`](./m-errorhandling-result-orerror.md): Replaces whatever error a result carries with the supplied typed error. <code>Ok</code> passes through unchanged.
- [`ErrorHandling.Result.fromTry`](./m-errorhandling-result-fromtry.md): Converts a .NET <code>Try*</code> tuple into a unit-error result.
- [`ErrorHandling.Result.fromChoice`](./m-errorhandling-result-fromchoice.md): Converts an F# <code>Choice</code> into a result.
- [`ErrorHandling.Result.toOption`](./m-errorhandling-result-tooption.md): Drops the error channel and returns <code>Some</code> for success.
- [`ErrorHandling.Result.toValueOption`](./m-errorhandling-result-tovalueoption.md): Drops the error channel and returns <code>ValueSome</code> for success.
- [`ErrorHandling.Result.defaultValue`](./m-errorhandling-result-defaultvalue.md): Returns the success value or the supplied fallback value.

## Extraction helpers

- [`ErrorHandling.Result.someOr`](./m-errorhandling-result-someor.md): Takes the value from an option when it is <code>Some</code>, or returns the supplied error.
- [`ErrorHandling.Result.noneOr`](./m-errorhandling-result-noneor.md): Returns success when the option is <code>None</code>, or returns the supplied error.
- [`ErrorHandling.Result.valueSomeOr`](./m-errorhandling-result-valuesomeor.md): Takes the value from a value option when it is <code>ValueSome</code>, or returns the supplied error.
- [`ErrorHandling.Result.valueNoneOr`](./m-errorhandling-result-valuenoneor.md): Returns success when the value option is <code>ValueNone</code>, or returns the supplied error.
- [`ErrorHandling.Result.nullableOr`](./m-errorhandling-result-nullableor.md): Takes the value from a nullable when it has a value, or returns the supplied error.
- [`ErrorHandling.Result.notNullOr`](./m-errorhandling-result-notnullor.md): Keeps a non-null reference, or returns the supplied error.
- [`ErrorHandling.Result.okOr`](./m-errorhandling-result-okor.md): Takes the successful value from a result, or returns the supplied error.
- [`ErrorHandling.Result.errorOr`](./m-errorhandling-result-erroror.md): Takes the error value from a result, or returns the supplied error when the result is successful.
- [`ErrorHandling.Result.headOr`](./m-errorhandling-result-heador.md): Takes the first item from a sequence, or returns the supplied error.

## Traversal

- [`ErrorHandling.Collection.traverseResult`](./m-errorhandling-collection-traverseresult.md): Maps each value with a result-returning function, stopping at the first error.
- [`ErrorHandling.Collection.sequenceResult`](./m-errorhandling-collection-sequenceresult.md): Turns a sequence of results into one fail-fast result containing all successes.

## Builder

- [`result`](./p-errorhandling--result.md): The fail-fast <code>result { }</code> computation expression.
