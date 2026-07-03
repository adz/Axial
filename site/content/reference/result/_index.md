---
title: "Result"
weight: 60
type: docs
---

This page shows Axial's fail-fast helpers over the standard F# `Result<'value, 'error>` type. Use `Result.require` when a `Check<'value>` should return `Result<unit, CheckFailure list>`, and use `Result.guard` or named guards such as `Result.notBlank` when a check should keep the original value. Extraction helpers such as `Result.someOr` change the success shape, while sequence helpers such as `Result.single` return focused diagnostics. The `result { }` builder sequences ordinary fail-fast `Result` workflows.

## Structured errors

- [`ErrorHandling.CardinalityFailure`](./t-errorhandling-cardinalityfailure.md): Structured errors returned by sequence cardinality helpers.
- [`ErrorHandling.CheckFailure`](./t-errorhandling-checkfailure.md): Describes why an executable value check failed, without attaching source paths or raw input.

## Core helpers

- [`ErrorHandling.Result.ok`](./m-errorhandling-result-ok.md): Creates an <code>Ok</code> result.
- [`ErrorHandling.Result.error`](./m-errorhandling-result-error.md): Creates an <code>Error</code> result.
- [`ErrorHandling.Result.map`](./m-errorhandling-result-map.md): Maps the success value of a result.
- [`ErrorHandling.Result.mapError`](./m-errorhandling-result-maperror.md): Maps the error value of a result.
- [`ErrorHandling.Result.bind`](./m-errorhandling-result-bind.md): Binds a result to the next fail-fast operation.

## Lifts and conversions

- [`ErrorHandling.Result.require`](./m-errorhandling-result-require.md): Runs a value check and returns <code>Ok ()</code> or the check failures.
- [`ErrorHandling.Result.guard`](./m-errorhandling-result-guard.md): Runs a value check and keeps the original input when it succeeds.
- [`ErrorHandling.Result.checkOr`](./m-errorhandling-result-checkor.md): Returns <code>Ok ()</code> when the condition is true, or the supplied error when it is false.
- [`ErrorHandling.Result.keepIf`](./m-errorhandling-result-keepif.md): Keeps the input value when the predicate is true, or returns the supplied error.
- [`ErrorHandling.Result.withError`](./m-errorhandling-result-witherror.md): Replaces a unit error with the supplied typed error.
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
- [`ErrorHandling.Result.single`](./m-errorhandling-result-single.md): Takes the only item from a sequence.
- [`ErrorHandling.Result.atMostOne`](./m-errorhandling-result-atmostone.md): Takes zero or one item from a sequence.

## Structural guards

- [`ErrorHandling.Result.notBlank`](./m-errorhandling-result-notblank.md): Keeps a non-null, non-empty, non-whitespace string.
- [`ErrorHandling.Result.length`](./m-errorhandling-result-length.md): Keeps a string whose length lies between the supplied inclusive bounds.
- [`ErrorHandling.Result.minLength`](./m-errorhandling-result-minlength.md): Keeps a string whose length is at least the supplied minimum.
- [`ErrorHandling.Result.maxLength`](./m-errorhandling-result-maxlength.md): Keeps a string whose length is at most the supplied maximum.
- [`ErrorHandling.Result.exactLength`](./m-errorhandling-result-exactlength.md): Keeps a string whose length equals the supplied expected length.
- [`ErrorHandling.Result.range`](./m-errorhandling-result-range.md): Keeps a value between the supplied inclusive bounds.
- [`ErrorHandling.Result.greaterThan`](./m-errorhandling-result-greaterthan.md): Keeps a value greater than the supplied exclusive lower bound.
- [`ErrorHandling.Result.lessThan`](./m-errorhandling-result-lessthan.md): Keeps a value less than the supplied exclusive upper bound.
- [`ErrorHandling.Result.atLeast`](./m-errorhandling-result-atleast.md): Keeps a value greater than or equal to the supplied lower bound.
- [`ErrorHandling.Result.atMost`](./m-errorhandling-result-atmost.md): Keeps a value less than or equal to the supplied upper bound.
- [`ErrorHandling.Result.atLeastOne`](./m-errorhandling-result-atleastone.md): Keeps a sequence that contains at least one item.
- [`ErrorHandling.Result.moreThanOne`](./m-errorhandling-result-morethanone.md): Keeps a sequence that contains more than one item.

## Traversal

- [`ErrorHandling.Collection.traverseResult`](./m-errorhandling-collection-traverseresult.md): Maps each value with a result-returning function, stopping at the first error.
- [`ErrorHandling.Collection.sequenceResult`](./m-errorhandling-collection-sequenceresult.md): Turns a sequence of results into one fail-fast result containing all successes.

## Builder

- [`result`](./p-errorhandling--result.md): The fail-fast <code>result { }</code> computation expression.
