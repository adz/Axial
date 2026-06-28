---
title: "Result"
weight: 60
type: docs
---

This page shows Axial's fail-fast helpers over the standard F# `Result<'value, 'error>` type. Use `Result.guard` or named guards such as `Result.notBlank` when a predicate should keep the original value, extraction helpers such as `Result.some` when the success shape changes, and structural helpers such as `Result.single` when a useful diagnostic is available. The `result { }` builder sequences ordinary fail-fast `Result` workflows.

## Structured errors

- [`ErrorHandling.CardinalityFailure`](./t-errorhandling-cardinalityfailure.md): Structured errors returned by sequence cardinality helpers.
- [`ErrorHandling.StringLengthFailure`](./t-errorhandling-stringlengthfailure.md): Structured errors returned by string length helpers.
- [`ErrorHandling.RangeFailure`](./t-errorhandling-rangefailure.md): Structured errors returned by comparison helpers.

## Core helpers

- [`ErrorHandling.Result.ok`](./m-errorhandling-result-ok.md): Creates an <code>Ok</code> result.
- [`ErrorHandling.Result.error`](./m-errorhandling-result-error.md): Creates an <code>Error</code> result.
- [`ErrorHandling.Result.map`](./m-errorhandling-result-map.md): Maps the success value of a result.
- [`ErrorHandling.Result.mapError`](./m-errorhandling-result-maperror.md): Maps the error value of a result.
- [`ErrorHandling.Result.bind`](./m-errorhandling-result-bind.md): Binds a result to the next fail-fast operation.

## Lifts and conversions

- [`ErrorHandling.Result.guard`](./m-errorhandling-result-guard.md): Lifts a predicate into a type-preserving result guard with the supplied error.
- [`ErrorHandling.Result.require`](./m-errorhandling-result-require.md): Turns a boolean condition into a unit-success result with the supplied error.
- [`ErrorHandling.Result.fromPredicate`](./m-errorhandling-result-frompredicate.md): Lifts a predicate into a unit-error result.
- [`ErrorHandling.Result.fromTry`](./m-errorhandling-result-fromtry.md): Converts a .NET <code>Try*</code> tuple into a unit-error result.
- [`ErrorHandling.Result.fromChoice`](./m-errorhandling-result-fromchoice.md): Converts an F# <code>Choice</code> into a result.
- [`ErrorHandling.Result.toOption`](./m-errorhandling-result-tooption.md): Drops the error channel and returns <code>Some</code> for success.
- [`ErrorHandling.Result.toValueOption`](./m-errorhandling-result-tovalueoption.md): Drops the error channel and returns <code>ValueSome</code> for success.
- [`ErrorHandling.Result.defaultValue`](./m-errorhandling-result-defaultvalue.md): Returns the success value or the supplied fallback value.

## Extraction helpers

- [`ErrorHandling.Result.some`](./m-errorhandling-result-some.md): Takes the value from an option when it is <code>Some</code>.
- [`ErrorHandling.Result.none`](./m-errorhandling-result-none.md): Returns success when the option is <code>None</code>.
- [`ErrorHandling.Result.valueSome`](./m-errorhandling-result-valuesome.md): Takes the value from a value option when it is <code>ValueSome</code>.
- [`ErrorHandling.Result.valueNone`](./m-errorhandling-result-valuenone.md): Returns success when the value option is <code>ValueNone</code>.
- [`ErrorHandling.Result.nullable`](./m-errorhandling-result-nullable.md): Takes the value from a nullable when it has a value.
- [`ErrorHandling.Result.okValue`](./m-errorhandling-result-okvalue.md): Takes the successful value from a result.
- [`ErrorHandling.Result.errorValue`](./m-errorhandling-result-errorvalue.md): Takes the error value from a result.
- [`ErrorHandling.Result.head`](./m-errorhandling-result-head.md): Takes the first item from a sequence.

## Structural guards

- [`ErrorHandling.Result.notBlank`](./m-errorhandling-result-notblank.md): Keeps a non-blank string or returns the supplied error.
- [`ErrorHandling.Result.notNull`](./m-errorhandling-result-notnull.md): Keeps a non-null reference or returns the supplied error.
- [`ErrorHandling.Result.notEmpty`](./m-errorhandling-result-notempty.md): Keeps a non-empty collection or returns the supplied error.
- [`ErrorHandling.Result.contains`](./m-errorhandling-result-contains.md): Keeps a collection that contains the expected value or returns the supplied error.
- [`ErrorHandling.Result.hasNoDuplicates`](./m-errorhandling-result-hasnoduplicates.md): Keeps a collection that contains no duplicate values or returns the supplied error.
- [`ErrorHandling.Result.length`](./m-errorhandling-result-length.md): Keeps a string whose length lies between the supplied inclusive bounds.
- [`ErrorHandling.Result.minLength`](./m-errorhandling-result-minlength.md): Keeps a string whose length is at least the supplied minimum.
- [`ErrorHandling.Result.maxLength`](./m-errorhandling-result-maxlength.md): Keeps a string whose length is at most the supplied maximum.
- [`ErrorHandling.Result.exactLength`](./m-errorhandling-result-exactlength.md): Keeps a string whose length equals the supplied expected length.
- [`ErrorHandling.Result.range`](./m-errorhandling-result-range.md): Keeps a value between the supplied inclusive bounds.
- [`ErrorHandling.Result.greaterThan`](./m-errorhandling-result-greaterthan.md): Keeps a value greater than the supplied exclusive lower bound.
- [`ErrorHandling.Result.lessThan`](./m-errorhandling-result-lessthan.md): Keeps a value less than the supplied exclusive upper bound.
- [`ErrorHandling.Result.atLeast`](./m-errorhandling-result-atleast.md): Keeps a value greater than or equal to the supplied lower bound.
- [`ErrorHandling.Result.atMost`](./m-errorhandling-result-atmost.md): Keeps a value less than or equal to the supplied upper bound.
- [`ErrorHandling.Result.single`](./m-errorhandling-result-single.md): Takes the only item from a sequence.
- [`ErrorHandling.Result.atMostOne`](./m-errorhandling-result-atmostone.md): Takes zero or one item from a sequence.
- [`ErrorHandling.Result.atLeastOne`](./m-errorhandling-result-atleastone.md): Keeps a sequence that contains at least one item.
- [`ErrorHandling.Result.moreThanOne`](./m-errorhandling-result-morethanone.md): Keeps a sequence that contains more than one item.

## Traversal

- [`ErrorHandling.Collection.traverseResult`](./m-errorhandling-collection-traverseresult.md): Maps each value with a result-returning function, stopping at the first error.
- [`ErrorHandling.Collection.sequenceResult`](./m-errorhandling-collection-sequenceresult.md): Turns a sequence of results into one fail-fast result containing all successes.

## Builder

- [`result`](./p-errorhandling--result.md): The fail-fast <code>result { }</code> computation expression.
